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

        public async Task<User> CreateUserAsync(User user)
        {
            //TODO Check for CanManageUserLicenses permission if user.LicenseType != null

            var validationResult = await _userValidator.ValidateAsync(user);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (IsBuiltInOnPremAccount(user.TenantId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(user.TenantId), "Users cannot be created under provisioning accounts") });
            }

            user.FirstName = user.FirstName.Trim();
            user.LastName = user.LastName.Trim();

            var result = await CreateUserInDb(user);

            await AssignUserLicense(user);

            _eventService.Publish(EventNames.UserCreated, result);

            return result;
        }
        
        public async Task<User> GetUserAsync(Guid id)
        {
            var validationResult = await _userIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _userRepository.GetItemAsync(id);

            if (result == null)
            {
                _logger.Warning($"A User resource could not be found for id {id}");
                throw new NotFoundException($"A User resource could not be found for id {id}");
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
                throw new ValidationFailedException(errors);
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
                throw new ValidationFailedException(validationResult.Errors);
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


        private bool IsBuiltInOnPremAccount(Guid accountId)
        {
            //TODO Identify if this is an on prem deployment
            //if (!_cloudSettings.DeploymentTypes.HasFlag(DeploymentTypes.OnPrem))
            {
                return false;
            }

            //return accountId.ToString().ToUpper() == "2D907264-8797-4666-A8BB-72FE98733385" ||
            //       accountId.ToString().ToUpper() == "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3";
        }


        private async Task<User> CreateUserInDb(User user)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueUsername(user.Id, user.UserName))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.UserName), "A user with that UserName already exists."));
            }

            if (!await IsUniqueEmail(user.Id, user.Email))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.Email), "A user with that email address already exists.") );
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.PasswordHash), "Password Hash and Salt can not be null") );
            }

            if (!String.IsNullOrEmpty(user.LdapId) && await IsUniqueLdapId(user.Id, user.LdapId))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.PasswordHash), "Unable to provision user. The LDAP User Account is already in use."));
            }

            //TODO Check if it is a valid account
            //var account = dc.Accounts.Find(accountId);
            //if (account == null)
            //{
            //    var ex = new Exception("Unable to provision user. The account could not be found with the given id.");
            //    LogError(ex);
            //    throw ex;
            //}

            if (validationErrors.Any())
            {
                throw new ValidationFailedException(validationErrors);
            }

            var result = await _userRepository.CreateItemAsync(user);

            //TODO Add user to Basic_user group

            // don't send down password hash or salt
            user.PasswordHash = null;
            user.PasswordSalt = null;

            return result;
        }

        private async Task AssignUserLicense(User user)
        {
            //TODO Assign user license 
            await Task.FromResult<int>(0);
        }


        private async Task<bool> IsUniqueUsername(Guid? userId, string username)
        {
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.UserName == username
                                                    : u.Id != userId && u.UserName == username);
            return !users.Any();
        }

        private async Task<bool> IsUniqueEmail(Guid? userId, string email)
        {
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.Email == email
                                                    : u.Id != userId && u.Email == email);
            return !users.Any();
        }

        private async Task<bool> IsUniqueLdapId(Guid? userId, string ldapId)
        {
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.LdapId == ldapId
                                                    : u.Id != userId && u.LdapId == ldapId);
            return !users.Any();
        }
    }
}