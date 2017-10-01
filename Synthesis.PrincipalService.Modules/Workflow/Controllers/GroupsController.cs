using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    /// <inheritdoc />
    /// <summary>
    /// Groups Controller Class.
    /// </summary>
    /// <seealso cref="T:Synthesis.PrincipalService.Workflow.Controllers.IGroupsController" />
    public class GroupsController : IGroupsController
    {
        private readonly IRepository<Group> _groupRepository;
        private readonly IValidator _createGroupValidator;
        private readonly IValidator _groupValidatorId;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupsController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="logger">The logger.</param>
        public GroupsController(IRepositoryFactory repositoryFactory,
                                IValidatorLocator validatorLocator,
                                IEventService eventService,
                                ILogger logger)
        {
            _groupRepository = repositoryFactory.CreateRepository<Group>();
            _createGroupValidator = validatorLocator.GetValidator(typeof(CreateGroupRequestValidator));
            _groupValidatorId = validatorLocator.GetValidator(typeof(GroupIdValidator));
            _eventService = eventService;
            _logger = logger;
        }

        /// <summary>
        /// Creates the group asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <returns>
        /// Group object.
        /// </returns>
        /// <exception cref="ValidationFailedException"></exception>
        public async Task<Group> CreateGroupAsync(Group model, Guid tenantId, Guid userId)
        {
            var validationResult = await _createGroupValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a Group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Do not allow creation of a group with IsLocked set to true unless you are a superadmin
            if (model.IsLocked && !IsSuperAdmin(userId))
            {
                model.IsLocked = false;
            }

            // Replace any fields in the DTO that shouldn't be changed here
            model.TenantId = tenantId;

            var result = await CreateGroupInDb(model);

            _eventService.Publish(EventNames.GroupCreated, result);
            return result;
        }

        public async Task<Guid> DeleteGroupAsync(Guid groupId)
        {
            var validationResult = await _groupValidatorId.ValidateAsync(groupId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to delete a Group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                await _groupRepository.DeleteItemAsync(groupId);

                _eventService.Publish(new ServiceBusEvent<Guid>
                {
                    Name = EventNames.GroupDeleted,
                    Payload = groupId
                });
                return groupId;
            }
            catch (DocumentNotFoundException)
            {
                // The resource not being there is what we wanted.
            }

            return Guid.Empty;
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

            if (!await IsUniqueGroup(group.Id, group.Name))
            {
                validationErrors.Add(new ValidationFailure(nameof(group.Name), "A group with that Group name already exists."));
            }

            if (validationErrors.Any())
            {
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
        /// <returns>Task object of true or false.</returns>
        private async Task<bool> IsUniqueGroup(Guid? groupId, string groupName)
        {
            var groups = await _groupRepository.GetItemsAsync(g => groupId == null || groupId.Value == Guid.Empty ? g.Name == groupName : g.Id != groupId && g.Name == groupName);
            return !groups.Any();
        }

        /// <summary>
        /// Determines whether [is super admin] [the specified user identifier].
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>
        ///   <c>true</c> if [is super admin] [the specified user identifier]; otherwise, <c>false</c>.
        /// </returns>
        private bool IsSuperAdmin(Guid userId)
        {
            //var userGroups = GetUserGroupsForUser(userId).Payload;
            //return userGroups.Any(x => x.GroupId.Equals(SuperAdminGroupId));
            //TODO: Put code here to check User Group here - Yusuf
            return true;
        }
    }
}
