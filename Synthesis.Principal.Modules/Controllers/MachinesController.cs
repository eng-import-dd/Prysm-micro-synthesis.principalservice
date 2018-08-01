using System;
using System.Collections.Generic;
using System.IdentityModel;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Represents a controller for Machine resources.
    /// </summary>
    /// <seealso cref="IMachineController" />
    public class MachinesController : IMachineController
    {
        private readonly IRepository<Machine> _machineRepository;
        private readonly IValidator _createMachineRequestValidator;
        private readonly IValidator _machineIdValidator;
        private readonly IValidator _tenantIdValidator;
        private readonly IValidator _updateMachineRequestValidator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly ICloudShim _cloudShim;

        /// <summary>
        /// Constructor for the MachinesController.
        /// </summary>
        /// <param name="repositoryFactory">The Repository Factory </param>
        /// <param name="validatorLocator">The Validator Locator </param>
        /// <param name="loggerFactory">The Logger Factory Object </param>
        /// <param name="eventService">The Event Service </param>
        /// <param name="cloudShim">The cloud api service</param>
        public MachinesController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            ILoggerFactory loggerFactory,
            IEventService eventService,
            ICloudShim cloudShim)
        {
            _machineRepository = repositoryFactory.CreateRepository<Machine>();
            _createMachineRequestValidator = validatorLocator.GetValidator(typeof(CreateMachineRequestValidator));
            _machineIdValidator = validatorLocator.GetValidator(typeof(MachineIdValidator));
            _tenantIdValidator = validatorLocator.GetValidator(typeof(TenantIdValidator));
            _updateMachineRequestValidator = validatorLocator.GetValidator(typeof(UpdateMachineRequestValidator));
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _cloudShim = cloudShim;
        }

        public async Task<Machine> CreateMachineAsync(Machine machine)
        {
            var validationResult = await _createMachineRequestValidator.ValidateAsync(machine);

            if (!validationResult.IsValid)
            {
                _logger.Error($"Validation failed while attempting to create a Machine resource. {validationResult.Errors}");
                throw new ValidationFailedException(validationResult.Errors);
            }

            machine.DateCreated = DateTime.UtcNow;
            machine.DateModified = DateTime.UtcNow;
            machine.Id = Guid.NewGuid();
            machine.NormalizedLocation = machine.Location.ToUpperInvariant();
            machine.MachineKey = machine.MachineKey.ToUpperInvariant();

            var result = await CreateMachineInDb(machine);

            _eventService.Publish(EventNames.MachineCreated, result);

            await _cloudShim.CopyMachineSettings(result.Id);

            return result;
        }

        public async Task<Machine> GetMachineByIdAsync(Guid machineId)
        {
            var machineIdValidationResult = await _machineIdValidator.ValidateAsync(machineId);
            if (!machineIdValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a Machine resource.");
                throw new ValidationFailedException(machineIdValidationResult.Errors);
            }

            var result = await _machineRepository.GetItemAsync(machineId);
            if (result == null)
            {
                _logger.Error($"A Machine resource could not be found for id {machineId}");
                throw new NotFoundException($"A Machine resource could not be found for id {machineId}");
            }

            return result;
        }

        public async Task<Machine> GetMachineByKeyAsync(string machineKey)
        {
            var normalizedMachineKey = machineKey.ToUpperInvariant();
            var result = await _machineRepository.GetItemsAsync(m => m.MachineKey.Equals(normalizedMachineKey));
            var machine = result.FirstOrDefault();
            if (machine == null)
            {
                _logger.Error($"A Machine resource could not be found for id {machineKey}");
                throw new NotFoundException($"A Machine resource could not be found for id {machineKey}");
            }

            return machine;
        }

        public async Task<Machine> UpdateMachineAsync(Machine model)
        {
            var validationResult = await _updateMachineRequestValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to update a Machine resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                var machine = model;
                machine.MachineKey = machine.MachineKey.ToUpperInvariant();
                machine.NormalizedLocation = machine.Location.ToUpperInvariant();
                return await UpdateMachineInDb(machine);
            }
            catch (DocumentNotFoundException ex)
            {
                _logger.Error("Could not find the Machine to update", ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("Could not Update the Machine.", ex);
                throw;
            }
        }

        private async Task<Machine> CreateMachineInDb(Machine machine)
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

            if (validationErrors.Any())
            {
                _logger.Error("An error occurred creating the machine");
                throw new ValidationFailedException(validationErrors);
            }

            var result = await _machineRepository.CreateItemAsync(machine);

            return result;
        }

        private async Task<Machine> UpdateMachineInDb(Machine machine)
        {
            var validationErrors = new List<ValidationFailure>();

            if (machine.Id == Guid.Empty)
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Machine Id was not provided."));
            }

            var existingMachine = await _machineRepository.GetItemAsync(machine.Id);

            if (existingMachine == null)
            {
                _logger.Error($"An error occurred updating machine because it does not exist: {machine.Id}");
                throw new NotFoundException("No Machine with id " + machine.Id + " was found.");
            }

            if (!await IsUniqueLocation(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Location was not unique"));
            }

            if (!await IsUniqueMachineKey(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Machine Key was not unique"));
            }

            if (validationErrors.Any())
            {
                _logger.Error($"A validation error occurred updating machine: {machine.Id}");
                throw new ValidationFailedException(validationErrors);
            }

            existingMachine.DateModified = DateTime.UtcNow;
            existingMachine.MachineKey = machine.MachineKey;
            existingMachine.Location = machine.Location;
            existingMachine.ModifiedBy = machine.ModifiedBy;
            existingMachine.SettingProfileId = machine.SettingProfileId;
            existingMachine.SynthesisVersion = machine.SynthesisVersion;
            existingMachine.LastOnline = machine.LastOnline;
            existingMachine.NormalizedLocation = machine.NormalizedLocation;

            try
            {
                await _machineRepository.UpdateItemAsync(machine.Id, existingMachine);
            }
            catch (Exception ex)
            {
                _logger.Error("Machine Update failed.", ex);
                throw;
            }

            return existingMachine;
        }

        public async Task DeleteMachineAsync(Guid machineId)
        {
            var machineIdValidationResult = await _machineIdValidator.ValidateAsync(machineId);

            if (!machineIdValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to delete a Machine resource.");
                throw new ValidationFailedException(machineIdValidationResult.Errors);
            }

            var result = await _machineRepository.GetItemAsync(machineId);
            if (result == null)
            {
                _logger.Error($"A Machine resource could not be found for id {machineId}");
                throw new NotFoundException($"A Machine resource could not be found for id {machineId}");
            }

            try
            {
                await _machineRepository.DeleteItemAsync(machineId);
            }
            catch (DocumentNotFoundException)
            {
                // Suppressing this exception for deletes
            }
        }

        public async Task<Machine> ChangeMachineTenantasync(Guid machineId, Guid tenantId, Guid settingProfileId)
        {
            var existingMachine = await _machineRepository.GetItemAsync(machineId);

            if (settingProfileId == Guid.Empty)
            {
                _logger.Error("An error occurred changing the tenant. settingProfileId must not be null or empty");
                throw new BadRequestException("Setting Profile Id cannot be null");
            }

            if (existingMachine == null)
            {
                _logger.Error($"An error occurred changing the tenant. Machine {machineId} was not found.");
                throw new NotFoundException($"No Machine with id {machineId} was found.");
            }

            if (!await IsValidSettingProfile(tenantId, settingProfileId))
            {
                _logger.Error("Invalid operation. The settings profile is not valid.");
                throw new InvalidOperationException();
            }

            existingMachine.SettingProfileId = settingProfileId;

            try
            {
                await _machineRepository.UpdateItemAsync(machineId, existingMachine);
            }
            catch (Exception ex)
            {
                _logger.Error("Could not move machine", ex);
                throw;
            }

            return existingMachine;
        }

        public async Task<List<Machine>> GetTenantMachinesAsync(Guid tenantId)
        {
            var tenantIdValidationResult = await _tenantIdValidator.ValidateAsync(tenantId);
            if (!tenantIdValidationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a Machines for tenant.");
                throw new ValidationFailedException(tenantIdValidationResult.Errors);
            }

            var result = await _machineRepository.GetItemsAsync(m => m.TenantId == tenantId);

            if (result == null)
            {
                _logger.Warning($"Machine resources could not be found for id {tenantId}");
                throw new NotFoundException($"Machine resources could not be found for id {tenantId}");
            }

            return result.ToList();
        }

        private async Task<bool> IsUniqueLocation(Machine machine)
        {
            var tenantMachines = await _machineRepository.GetItemsAsync(m => m.TenantId == machine.TenantId && (m.NormalizedLocation == machine.NormalizedLocation && m.Id != machine.Id));
            return tenantMachines.Any() == false;
        }

        private async Task<bool> IsUniqueMachineKey(Machine machine)
        {
            var machinesWithMatchingMachinesKey = await _machineRepository.GetItemsAsync(m => m.MachineKey == machine.MachineKey && (m.Id != machine.Id));
            return machinesWithMatchingMachinesKey.Any() == false;
        }

        private async Task<bool> IsValidSettingProfile(Guid tenantId, Guid settingProfileId)
        {
            var result = await _cloudShim.ValidateSettingProfileId(tenantId, settingProfileId);
            return result.Payload;
        }
    }
}
