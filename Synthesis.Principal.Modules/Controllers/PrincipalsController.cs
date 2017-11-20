using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers.Interfaces;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Validators;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    ///     Represents a controller for Principal resources.
    /// </summary>
    /// <seealso cref="IPrincipalsController" />
    public class PrincipalsController : IPrincipalsController
    {
        private readonly IEventService _eventService;
        // ReSharper disable once NotAccessedField.Local
        private readonly ILogger _logger;
        private readonly IRepository<Principal> _principalRepository;
        private readonly IValidatorLocator _validatorLocator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PrincipalsController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public PrincipalsController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory)
        {
            _principalRepository = repositoryFactory.CreateRepository<Principal>();
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
        }

        public async Task<Principal> CreatePrincipalAsync(Principal model)
        {
            var validationResult = _validatorLocator.Validate<PrincipalValidator>(model);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _principalRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.PrincipalCreated, result);

            return result;
        }

        public async Task DeletePrincipalAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<PrincipalIdValidator>(id);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                await _principalRepository.DeleteItemAsync(id);

                _eventService.Publish(new ServiceBusEvent<Guid>
                {
                    Name = EventNames.PrincipalDeleted,
                    Payload = id
                });
            }
            catch (DocumentNotFoundException)
            {
                // We don't really care if it's not found.
                // The resource not being there is what we wanted.
            }
        }

        public async Task<Principal> GetPrincipalAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<PrincipalIdValidator>(id);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _principalRepository.GetItemAsync(id);

            if (result == null)
            {
                throw new NotFoundException($"A Principal resource could not be found for id {id}");
            }

            return result;
        }

        public async Task<Principal> UpdatePrincipalAsync(Guid principalId, Principal principalModel)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(PrincipalIdValidator), principalId },
                { typeof(PrincipalValidator), principalModel }
            });
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                var result = await _principalRepository.UpdateItemAsync(principalId, principalModel);

                _eventService.Publish(EventNames.PrincipalUpdated, result);

                return result;
            }
            catch (DocumentNotFoundException)
            {
                throw new NotFoundException($"A Principal resource could not be found for id {principalId}");
            }
        }
    }
}
