﻿using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Utilities;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    /// <summary>
    /// Represents a controller for Machine resources.
    /// </summary>
    /// <seealso cref="Synthesis.PrincipalService.Workflow.Controllers.IMachineController" />
    public class MachinesController : IMachineController
    {
        private readonly IRepository<Machine> _machineRepository;
        private readonly IValidator _createMachineRequestValidator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        /// <summary>
        /// Constructor for the MachinesController.
        /// </summary>
        /// <param name="repositoryFactory">The Repository Factory </param>
        /// <param name="validatorLocator">The Validator Locator </param>
        /// <param name="logger">The Logger Object </param>
        /// <param name="mapper">The Mapper Object </param>
        /// <param name="eventService">The Event Service </param>
        public MachinesController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            ILogger logger,
            IMapper mapper,
            IEventService eventService)
        {
            _machineRepository = repositoryFactory.CreateRepository<Machine>();

            _createMachineRequestValidator = validatorLocator.GetValidator(typeof(CreateMachineRequestValidator));
            _eventService = eventService;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<MachineResponse> CreateMachineAsync(CreateMachineRequest model)
        {
            var validationResult = await _createMachineRequestValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a Machine resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var machine = _mapper.Map<CreateMachineRequest, Machine>(model);

            var result = await CreateMachineInDB(machine);

            await _eventService.PublishAsync(EventNames.MachineCreated);

            // TODO: Call to CopyMachineSettings. To be implemented as a service call to SettingsService.

            return _mapper.Map<Machine, MachineResponse>(result);
        }

        private async Task<Machine> CreateMachineInDB(Machine machine)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueLocation(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Location was not unique"));
            }

            if (!await IsUniqueMachineKey(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Machine Key was not unique"));
            }

            if(validationErrors.Any())
            {
                throw new ValidationFailedException(validationErrors);
            }

            machine.DateCreated = DateTime.UtcNow;
            machine.DateModified = DateTime.UtcNow;

            var result = await _machineRepository.CreateItemAsync(machine);

            return result;
        }

        private async Task<bool> IsUniqueLocation(Machine machine)
        {
            var accountMachines = await _machineRepository.GetItemsAsync(m => m.TenantId == machine.TenantId && (m.Location == machine.Location && m.Id == machine.Id));
            return accountMachines.Any() == false;
        }

        private async Task<bool> IsUniqueMachineKey(Machine machine)
        {
            var machinesWithMatchingMachinesKey = await _machineRepository.GetItemsAsync(m => m.MachineKey == machine.MachineKey && (m.Id != machine.Id));
            return machinesWithMatchingMachinesKey.Any() == false;
        }
    }
}
