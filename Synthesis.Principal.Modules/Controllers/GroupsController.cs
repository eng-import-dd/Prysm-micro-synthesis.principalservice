using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Services;
using Synthesis.PrincipalService.Validators;
using Synthesis.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    /// <inheritdoc />
    /// <summary>
    /// Groups Controller Class.
    /// </summary>
    /// <seealso cref="IGroupsController" />
    public class GroupsController : IGroupsController
    {
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly IValidatorLocator _validatorLocator;
        private readonly ISuperAdminService _superAdminService;
        private readonly AsyncLazy<IRepository<Group>> _groupRepositoryAsyncLazy;
        private readonly AsyncLazy<IRepository<User>> _userRepositoryAsyncLazy;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupsController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="superAdminService"></param>
        /// <param name="loggerFactory">The logger factory.</param>
        public GroupsController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ISuperAdminService superAdminService,
            ILoggerFactory loggerFactory)
        {
            _groupRepositoryAsyncLazy = new AsyncLazy<IRepository<Group>>(() => repositoryFactory.CreateRepositoryAsync<Group>());
            _userRepositoryAsyncLazy = new AsyncLazy<IRepository<User>>(() => repositoryFactory.CreateRepositoryAsync<User>());
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _superAdminService = superAdminService;
            _logger = loggerFactory.GetLogger(this);
        }

        public async Task CreateBuiltInGroupsAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            await Task.WhenAll(
                CreateBuiltInGroupAsync(tenantId, GroupType.Basic, GroupNames.Basic, cancellationToken),
                CreateBuiltInGroupAsync(tenantId, GroupType.TenantAdmin, GroupNames.TenantAdmin, cancellationToken));

            _eventService.Publish(EventNames.BuiltInGroupsCreatedForTenant, new GuidEvent(tenantId));
        }

        public async Task<Group> CreateGroupAsync(Group model, Guid tenantId, Guid currentUserId, CancellationToken cancellationToken)
        {
            var isBuiltInGroup = model.Type != GroupType.Custom;

            var validationResult = _validatorLocator.Validate<CreateGroupRequestValidator>(model);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Locked groups can only be created by SuperAdmins (unless we're internally creating a
            // built-in group)
            if (model.IsLocked && !isBuiltInGroup && (currentUserId != Guid.Empty || !await _superAdminService.IsSuperAdminAsync(currentUserId)))
            {
                model.IsLocked = false;
            }

            // Replace any fields in the DTO that shouldn't be changed here
            model.TenantId = tenantId;

            var result = await CreateGroupInDbAsync(model, cancellationToken);

            _eventService.Publish(EventNames.GroupCreated, result);
            return result;
        }

        public async Task<Group> GetGroupByIdAsync(Guid groupId, Guid tenantId, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<GroupIdValidator>(groupId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var groupRepository = await _groupRepositoryAsyncLazy;
            var queryOptions = new QueryOptions { PartitionKey = new PartitionKey(tenantId) };
            var result = await groupRepository.GetItemAsync(groupId, queryOptions, cancellationToken);
            if (result == null)
            {
                throw new NotFoundException($"A Group resource could not be found for id {groupId}");
            }

            return result;
        }

        public async Task<bool> DeleteGroupAsync(Guid groupId, Guid tenantId, Guid currentUserId, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<GroupIdValidator>(groupId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var groupRepository = await _groupRepositoryAsyncLazy;
            var existingGroup = await groupRepository.GetItemAsync(groupId, new QueryOptions { PartitionKey = new PartitionKey(tenantId) }, cancellationToken);
            if (existingGroup == null)
            {
                throw new NotFoundException($"Unable to find group with identifier {groupId} in tenant {tenantId}");
            }

            if (existingGroup.IsLocked && !await _superAdminService.IsSuperAdminAsync(currentUserId))
            {
                return false;
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var usersWithGroupToBeDeleted = await userRepository.CreateItemQuery(UsersController.DefaultBatchOptions)
                .Where(u => u.Groups.Contains(groupId))
                .ToListAsync(cancellationToken);

            foreach (var user in usersWithGroupToBeDeleted)
            {
                user.Groups.Remove(groupId);
                await userRepository.UpdateItemAsync(user.Id.GetValueOrDefault(), user, null, cancellationToken);
            }

            await groupRepository.DeleteItemAsync(groupId, new QueryOptions { PartitionKey = new PartitionKey(tenantId) }, cancellationToken);

            _eventService.Publish(new ServiceBusEvent<Guid>
            {
                Name = EventNames.GroupDeleted,
                Payload = groupId
            });
            return true;
        }

        public async Task<Group> UpdateGroupAsync(Group group, Guid currentUserId, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<UpdateGroupRequestValidator>(group);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var groupRepository = await _groupRepositoryAsyncLazy;

            // Some groups don't have a tenant ID (e.g. the Default group). Those groups will be
            // stored in the 'null' partition key.
            var queryOptions = new QueryOptions { PartitionKey = group.TenantId.HasValue ? new PartitionKey(group.TenantId.Value) : new PartitionKey(null) };
            var existingGroup = await groupRepository.GetItemAsync(group.Id.GetValueOrDefault(), queryOptions, cancellationToken);
            if (existingGroup == null)
            {
                throw new NotFoundException($"A group with the identifier '{group.Id}' in tenant '{group.TenantId?.ToString() ?? "null"}' could not be found");
            }

            var isSuperAdmin = await _superAdminService.IsSuperAdminAsync(currentUserId);
            // Only SuperAdmins can edit a locked group or the Default group (no tenant)
            if ((existingGroup.IsLocked || !group.TenantId.HasValue) && !isSuperAdmin)
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(group.IsLocked), "The group is locked and cannot be edited") });
            }

            // Only SuperAdmins can change the lock state of a group.
            if (group.IsLocked != existingGroup.IsLocked && !isSuperAdmin)
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(group.IsLocked), "The group lock state cannot be changed") });
            }

            // The Type property cannot be changed.
            group.Type = existingGroup.Type;

            var result = await groupRepository.UpdateItemAsync(group.Id.GetValueOrDefault(), group, null, cancellationToken);

            _eventService.Publish(EventNames.GroupUpdated, result);

            return result;
        }

        public async Task<IEnumerable<Group>> GetGroupsForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            // A list of the Groups which belong to the current user's tenant (excluding the SuperAdmin group)
            var groupRepository = await _groupRepositoryAsyncLazy;
            return await groupRepository.CreateItemQuery()
                .Where(g => g.TenantId == tenantId && g.Type != GroupType.Default)
                .ToListAsync(cancellationToken);
        }

        public async Task<Guid?> GetTenantIdForGroupIdAsync(Guid groupId, Guid? tenantId, CancellationToken cancellationToken)
        {
            var groupRepository = await _groupRepositoryAsyncLazy;
            var batchOptions = !tenantId.HasValue
                ? new BatchOptions { PartitionKey = new PartitionKey(null) }
                : tenantId.Value == Guid.Empty
                    ? new BatchOptions { EnableCrossPartitionQuery = true }
                    : new BatchOptions { PartitionKey = new PartitionKey(tenantId.Value) };

            return await groupRepository.CreateItemQuery(batchOptions)
                .Where(g => g.Id == groupId)
                .Select(g => g.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task CreateBuiltInGroupAsync(Guid tenantId, GroupType type, string groupName, CancellationToken cancellationToken)
        {
            try
            {
                var groupRepository = await _groupRepositoryAsyncLazy;
                if (await groupRepository.CreateItemQuery().AnyAsync(g => g.TenantId == tenantId && g.Type == type, cancellationToken))
                {
                    // The built-in group type already exists in this tenant.
                    return;
                }

                await CreateGroupAsync(
                    new Group
                    {
                        TenantId = tenantId,
                        Name = groupName,
                        Type = type,
                        IsLocked = true
                    },
                    tenantId,
                    Guid.Empty,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create '{groupName}' built-in group for {tenantId}", ex);
                throw;
            }
        }

        private async Task<Group> CreateGroupInDbAsync(Group group, CancellationToken cancellationToken)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueGroupAsync(group.Id, group.Name, group.TenantId))
            {
                validationErrors.Add(new ValidationFailure(nameof(group.Name), "A group with that Group name already exists."));
            }

            if (validationErrors.Any())
            {
                throw new ValidationFailedException(validationErrors);
            }

            var groupRepository = await _groupRepositoryAsyncLazy;
            var result = await groupRepository.CreateItemAsync(group, cancellationToken);
            return result;
        }

        private async Task<bool> IsUniqueGroupAsync(Guid? groupId, string groupName, Guid? tenantId)
        {
            var groupRepository = await _groupRepositoryAsyncLazy;
            return !await groupRepository.CreateItemQuery()
                .AnyAsync(g => g.Name == groupName && g.TenantId == tenantId && (groupId == null || groupId.Value == Guid.Empty || g.Id != groupId));
        }
    }
}