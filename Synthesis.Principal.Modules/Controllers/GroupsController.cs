using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Extensions;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Services;
using Synthesis.PrincipalService.Validators;

namespace Synthesis.PrincipalService.Controllers
{
    /// <inheritdoc />
    /// <summary>
    /// Groups Controller Class.
    /// </summary>
    /// <seealso cref="T:Synthesis.PrincipalService.Workflow.Controllers.IGroupsController" />
    public class GroupsController : IGroupsController
    {
        private readonly IRepository<Group> _groupRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly IValidatorLocator _validatorLocator;
        private readonly ISuperAdminService _superAdminService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupsController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="superAdminService"></param>
        /// <param name="loggerFactory">The logger factory.</param>
        public GroupsController(IRepositoryFactory repositoryFactory,
                                IValidatorLocator validatorLocator,
                                IEventService eventService,
                                ISuperAdminService superAdminService,
                                ILoggerFactory loggerFactory)
        {
            _groupRepository = repositoryFactory.CreateRepository<Group>();
            _userRepository = repositoryFactory.CreateRepository<User>();
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _superAdminService = superAdminService;
            _logger = loggerFactory.GetLogger(this);
        }

        public async Task CreateBuiltInGroupsAsync(Guid tenantId)
        {
            await Task.WhenAll(
                CreateBuiltInGroupAsync(tenantId, GroupType.Basic, GroupNames.Basic),
                CreateBuiltInGroupAsync(tenantId, GroupType.TenantAdmin, GroupNames.TenantAdmin));

            _eventService.Publish(EventNames.BuiltInGroupsCreatedForTenant, new GuidEvent(tenantId));
        }

        private async Task CreateBuiltInGroupAsync(Guid tenantId, GroupType type, string groupName)
        {
            try
            {
                await CreateGroupAsync(new Group
                {
                    TenantId = tenantId,
                    Name = groupName,
                    Type = type,
                    IsLocked = true
                }, tenantId, Guid.Empty, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error creating {groupName} group for {tenantId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Creates the group asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="currentUserId">The user identifier.</param>
        /// <param name="isBuiltInGroup"></param>
        /// <returns>
        /// Group object.
        /// </returns>
        /// <exception cref="ValidationFailedException"></exception>
        public async Task<Group> CreateGroupAsync(Group model, Guid tenantId, Guid currentUserId, bool isBuiltInGroup)
        {
            if (!isBuiltInGroup)
            {
                model.Type = GroupType.Custom;
            }

            var validationResult = await _validatorLocator.GetValidator(typeof(CreateGroupRequestValidator)).ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a Group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Do not allow creation of a group with IsLocked set to true unless you are a superadmin
            if (model.IsLocked && (currentUserId != Guid.Empty || !await _superAdminService.IsSuperAdminAsync(currentUserId)))
            {
                model.IsLocked = false;
            }

            // Replace any fields in the DTO that shouldn't be changed here
            model.TenantId = tenantId;

            var result = await CreateGroupInDb(model);

            _eventService.Publish(EventNames.GroupCreated, result);
            return result;
        }

        public async Task<Group> GetGroupByIdAsync(Guid groupId, Guid tenantId)
        {
            var validationResult = await _validatorLocator.GetValidator(typeof(GroupIdValidator)).ValidateAsync(groupId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a Group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _groupRepository.GetItemAsync(groupId);
            if (result == null)
            {
                _logger.Error($"A Group resource could not be found for id {groupId}");
                throw new NotFoundException($"A Group resource could not be found for id {groupId}");
            }

            var assignedTenantId = result.TenantId;
            if (assignedTenantId == Guid.Empty || assignedTenantId != tenantId)
            {
                throw new UnauthorizedAccessException();
            }

            return result;
        }

        public async Task<bool> DeleteGroupAsync(Guid groupId, Guid userId)
        {
            var validationResult = await _validatorLocator.GetValidator(typeof(GroupIdValidator)).ValidateAsync(groupId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to delete a Group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var groupToBeDeleted = await _groupRepository.GetItemAsync(groupId);
            if (groupToBeDeleted.IsLocked && ! await _superAdminService.IsSuperAdminAsync(userId))
            {
                _logger.Error("Cannot delete a locked group since user is not SuperAdmin.");
                return false;
            }

            var usersWithGroupToBeDeleted = await _userRepository.GetItemsAsync(u => u.Groups.Contains(groupId));
            foreach (var user in usersWithGroupToBeDeleted)
            {
                user.Groups.Remove(groupId);
                await _userRepository.UpdateItemAsync(user.Id ?? Guid.Empty, user);
            }

            await _groupRepository.DeleteItemAsync(groupId);

            _eventService.Publish(new ServiceBusEvent<Guid>
            {
                Name = EventNames.GroupDeleted,
                Payload = groupId
            });
            return true;
        }

        public async Task<Group> UpdateGroupAsync(Group model, Guid tenantId, Guid userId)
        {
            var validationResult = await _validatorLocator.GetValidator(typeof(UpdateGroupRequestValidator)).ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to update an existing Group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var existingGroupInDb = await _groupRepository.GetItemAsync(model.Id.ToGuid());
            if (existingGroupInDb != null)
            {
                if ((existingGroupInDb.IsLocked || model.IsLocked) && !await _superAdminService.IsSuperAdminAsync(userId))
                {
                    _logger.Error("Invalid operation. Locked groups cannot be edited.");
                    throw new InvalidOperationException("You can not edit a locked group");
                }
            }
            else
            {
                var errorMessage = $"Group not found with id {model.Id}";

                _logger.Error(errorMessage);
                throw new NotFoundException(errorMessage);
            }

            // Replace any fields in the DTO that shouldn't be changed here
            model.TenantId = tenantId;
            model.Type = existingGroupInDb.Type;
            var result = await _groupRepository.UpdateItemAsync(model.Id.ToGuid(), model);

            _eventService.Publish(EventNames.GroupUpdated, result);

            return result;
        }

        public async Task<IEnumerable<Group>> GetGroupsForTenantAsync(Guid tenantId, Guid userId)
        {
            //A list of the Groups which belong to the current user's tenant

            //TODO: Checks here - Yusuf, CU-577 added to address this
            // Legacy code - public List<GroupDTO> GetGroupsForAccount(Guid accountId) - In DatabaseService.cs
            // In legacy cloud service there is usage of GroupPermissions and Permissions tables to determine the accessibility.

            //Super Admin check
            // Legacy code
            /*
             * var superAdminGroup = result.Payload.FirstOrDefault(x => x.GroupId == CollaborationService.SuperAdminGroupId);
                if(superAdminGroup != null && !CollaborationService.IsSuperAdmin(UserId))
                {
                    result.Payload.Remove(superAdminGroup);
                }
             */

            var result = await _groupRepository.GetItemsAsync(g => g.TenantId == tenantId && g.Type != GroupType.Default);

            if (result == null)
            {
                _logger.Error($"A Group resource could not be found for tenant id {tenantId}");
                throw new NotFoundException($"A Group resource could not be found for tenant id {tenantId}");
            }

            return result;
        }

        /// <summary>
        /// Creates the group in database.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <returns>
        /// Group Object.
        /// </returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<Group> CreateGroupInDb(Group group)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueGroup(group.Id, group.Name, group.TenantId))
            {
                validationErrors.Add(new ValidationFailure(nameof(group.Name), "A group with that Group name already exists."));
            }

            if (validationErrors.Any())
            {
                _logger.Error($"A validation error occurred creating group {group.Id}");
                throw new ValidationFailedException(validationErrors);
            }

            var result = await _groupRepository.CreateItemAsync(group);
            return result;
        }

        /// <summary>
        /// Determines whether [is unique group] [the specified group identifier].
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="groupName">Name of the group.</param>
        /// <param name="tenantId"></param>
        /// <returns>Task object of true or false.</returns>
        private async Task<bool> IsUniqueGroup(Guid? groupId, string groupName, Guid? tenantId)
        {
            var groups = await _groupRepository.GetItemsAsync(g => g.Name == groupName && g.TenantId == tenantId && (groupId == null || groupId.Value == Guid.Empty || g.Id != groupId));
            return !groups.Any();
        }
    }
}
