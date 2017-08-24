using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.Nancy.MicroService;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    /// <summary>
    /// Represents a controller for User resources.
    /// </summary>
    /// <seealso cref="Synthesis.PrincipalService.Workflow.Controllers.IUsersController" />
    public class UsersController : IUsersController
    {
        private readonly IRepository<User> _userRepository;
        private readonly IValidator _userValidator;
        private readonly IValidator _userIdValidator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="logger">The logger.</param>
        public UsersController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger)
        {
            _userRepository = repositoryFactory.CreateRepository<User>();
            _userValidator = validatorLocator.GetValidator(typeof(UserValidator));
            _userIdValidator = validatorLocator.GetValidator(typeof(UserIdValidator));
            _eventService = eventService;
            _logger = logger;
        }

        public async Task<User> CreateUserAsync(User model)
        {
            var validationResult = await _userValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a User resource.");
                ValidationFailedException.Raise<User>(validationResult.Errors);
            }

            var result = await _userRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.UserCreated, result);

            return result;
        }

        public async Task<User> GetUserAsync(Guid id)
        {
            var validationResult = await _userIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a User resource.");
                ValidationFailedException.Raise<User>(validationResult.Errors);
            }

            var result = await _userRepository.GetItemAsync(id);

            if (result == null)
            {
                _logger.Warning($"A User resource could not be found for id {id}");
                NotFoundException.Raise();
            }

            return result;
        }

        public async Task<User> UpdateUserAsync(Guid userId, User userModel)
        {
            var userIdValidationResult = await _userIdValidator.ValidateAsync(userId);
            var userValidationResult = await _userValidator.ValidateAsync(userModel);
            var errors = new List<ValidationFailure>();

            if (!userIdValidationResult.IsValid)
            {
                errors.AddRange(userIdValidationResult.Errors);
            }

            if (!userValidationResult.IsValid)
            {
                errors.AddRange(userValidationResult.Errors);
            }

            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to update a User resource.");
                ValidationFailedException.Raise<User>(errors);
            }

            try
            {
                return await _userRepository.UpdateItemAsync(userId, userModel);
            }
            catch (DocumentNotFoundException)
            {
                return null;
            }
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var validationResult = await _userIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to delete a User resource.");
                ValidationFailedException.Raise<User>(validationResult.Errors);
            }

            try
            {
                await _userRepository.DeleteItemAsync(id);

                _eventService.Publish(new ServiceBusEvent<Guid>
                {
                    Name = EventNames.UserDeleted,
                    Payload = id
                });
            }
            catch (DocumentNotFoundException)
            {
                // We don't really care if it's not found.
                // The resource not being there is what we wanted.
            }
        }
    }
}
