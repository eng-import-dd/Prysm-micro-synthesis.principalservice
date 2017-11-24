using System;
using System.Collections.Generic;
using System.IdentityModel;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
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
        private readonly IMapper _mapper;

        /// <summary>
        /// Constructor for the MachinesController.
        /// </summary>
        /// <param name="repositoryFactory">The Repository Factory </param>
        /// <param name="validatorLocator">The Validator Locator </param>
        /// <param name="loggerFactory">The Logger Factory Object </param>
        /// <param name="mapper">The Mapper Object </param>
        /// <param name="eventService">The Event Service </param>
        public MachinesController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            ILoggerFactory loggerFactory,
            IMapper mapper,
            IEventService eventService)
        {
            _machineRepository = repositoryFactory.CreateRepository<Machine>();
            _createMachineRequestValidator = validatorLocator.GetValidator(typeof(CreateMachineRequestValidator));
            _machineIdValidator = validatorLocator.GetValidator(typeof(MachineIdValidator));
            _tenantIdValidator = validatorLocator.GetValidator(typeof(TenantIdValidator));
            _updateMachineRequestValidator = validatorLocator.GetValidator(typeof(UpdateMachineRequestValidator));
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _mapper = mapper;
        }

        public async Task<MachineResponse> CreateMachineAsync(CreateMachineRequest model, Guid tenantId)
        {
            var validationResult = await _createMachineRequestValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                _logger.Error($"Validation failed while attempting to create a Machine resource. {validationResult.Errors}");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var machine = _mapper.Map<CreateMachineRequest, Machine>(model);
            machine.TenantId = tenantId;
            machine.DateCreated = DateTime.UtcNow;
            machine.DateModified = DateTime.UtcNow;
            machine.Id = Guid.NewGuid();

            var result = await CreateMachineInDB(machine);

            await _eventService.PublishAsync(EventNames.MachineCreated);

            // TODO: Call to CopyMachineSettings. To be implemented as a service call to SettingsService.

            return _mapper.Map<Machine, MachineResponse>(result);
        }

        public async Task<MachineResponse> GetMachineByIdAsync(Guid machineId, Guid tenantId)
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

            var assignedTenantId = result.TenantId;
            if (assignedTenantId == Guid.Empty || assignedTenantId != tenantId)
            {
                _logger.Error($"Invalid operation. Machine {machineId} does not belong to tenant {tenantId}.");
                throw new InvalidOperationException();
            }
            return _mapper.Map<Machine, MachineResponse>(result);
        }

        public async Task<MachineResponse> GetMachineByKeyAsync(String machineKey, Guid tenantId)
        {
            var result = await _machineRepository.GetItemsAsync(m => m.MachineKey.Equals(machineKey));
            var machine = result.FirstOrDefault();
            if (machine == null)
            {
                _logger.Error($"A Machine resource could not be found for id {machineKey}");
                throw new NotFoundException($"A Machine resource could not be found for id {machineKey}");
            }

            var assignedTenantId = machine.TenantId;
            if (assignedTenantId == Guid.Empty || assignedTenantId != tenantId)
            {
                _logger.Error($"Invalid operation. Machine {machineKey} does not belong to tenant {tenantId}.");
                throw new InvalidOperationException();
            }
            return _mapper.Map<Machine, MachineResponse>(machine);
        }

        public async Task<MachineResponse> UpdateMachineAsync(UpdateMachineRequest model, Guid tenantId)
        {
            var validationResult = await _updateMachineRequestValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to update a Machine resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                var machine = _mapper.Map<UpdateMachineRequest, Machine>(model);
                return await UpdateMachineInDb(machine, tenantId);
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

            if (validationErrors.Any())
            {
                _logger.Error("An error occurred creating the machine");
                throw new ValidationFailedException(validationErrors);
            }

            var result = await _machineRepository.CreateItemAsync(machine);

            return result;
        }

        private async Task<MachineResponse> UpdateMachineInDb(Machine machine, Guid tenantId)
        {
            var validationErrors = new List<ValidationFailure>();

            if (machine.Id == null)
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Machine Id was not provided."));
            }

            var existingMachine = await _machineRepository.GetItemAsync(machine.Id);

            if (existingMachine == null)
            {
                _logger.Error($"An error occurred updating machine because it does not exist: {machine.Id}");
                throw new NotFoundException("No Machine with id " + machine.Id + " was found.");
            }

            if (existingMachine.TenantId != tenantId)
            {
                _logger.Error($"Invalid operation. Machine {machine.Id} does not belong to tenant {tenantId}.");
                throw new InvalidOperationException();
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

            try
            {
                await _machineRepository.UpdateItemAsync(machine.Id, existingMachine);
            }
            catch (Exception ex)
            {
                _logger.Error("Machine Update failed.", ex);
                throw;
            }

            return _mapper.Map<Machine, MachineResponse>(existingMachine);
        }

        public async Task DeleteMachineAsync(Guid machineId, Guid tenantId)
        {
            var machineIdValidationResult = await _machineIdValidator.ValidateAsync(machineId);

            if (!machineIdValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to delete a Machine resource.");
                throw new ValidationFailedException(machineIdValidationResult.Errors);
            }

            if (!IsUserASuperAdmin(tenantId))
            {
                _logger.Error("Could not delete machine. Current user is not a super admin.");
                throw new InvalidOperationException();
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

        public async Task<MachineResponse> ChangeMachineAccountAsync(Guid machineId, Guid tenantId, Guid settingProfileId)
        {
            var existingMachine = await _machineRepository.GetItemAsync(machineId);

            if (settingProfileId == null || settingProfileId == Guid.Empty)
            {
                _logger.Error("An error occurred changing the account. settingProfileId must not be null or empty");
                throw new BadRequestException("Setting Profile Id cannot be null");
            }

            if (existingMachine == null)
            {
                _logger.Error($"An error occurred changing the account. Machine {machineId} was not found.");
                throw new NotFoundException($"No Machine with id {machineId} was found.");
            }

            if (!IsUserASuperAdmin(tenantId))
            {
                _logger.Error("Invalid operation. Current user is not a super admin.");
                throw new InvalidOperationException();
            }

            // TODO: Validate that the SettingProfile provided is valid
            if (!IsValidSettingProfile(settingProfileId))
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

            return _mapper.Map<Machine, MachineResponse>(existingMachine);
        }

        public async Task<List<MachineResponse>> GetTenantMachinesAsync(Guid tenantId)
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

            return _mapper.Map<List<Machine>, List<MachineResponse>>(result.ToList());
        }

        private bool IsUserASuperAdmin(Guid id)
        {
            // To be replaced by a call to Settings Service(?) determining if the user is a superadmin user.
            return true;
        }

        private async Task<bool> IsUniqueLocation(Machine machine)
        {
            var accountMachines = await _machineRepository.GetItemsAsync(m => m.TenantId == machine.TenantId && (m.Location == machine.Location && m.Id != machine.Id));
            return accountMachines.Any() == false;
        }

        private async Task<bool> IsUniqueMachineKey(Machine machine)
        {
            var machinesWithMatchingMachinesKey = await _machineRepository.GetItemsAsync(m => m.MachineKey == machine.MachineKey && (m.Id != machine.Id));
            return machinesWithMatchingMachinesKey.Any() == false;
        }

        private bool IsValidSettingProfile(Guid settingProfileId)
        {
            // To be replaced by a call to Settings Service(?) determining if the SettingProfile is valid.
            return true;
        }
    }
}
