using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Services;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Represents a controller for Machine resources.
    /// </summary>
    /// <seealso cref="IMachinesController" />
    public class MachinesController : IMachinesController
    {
        private readonly AsyncLazy<IRepository<Machine>> _machineRepositoryAsyncLazy;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly ICloudShim _cloudShim;
        private readonly ITenantApi _tenantApi;

        /// <summary>
        /// Constructor for the MachinesController.
        /// </summary>
        /// <param name="repositoryFactory">The Repository Factory </param>
        /// <param name="validatorLocator">The Validator Locator </param>
        /// <param name="loggerFactory">The Logger Factory Object </param>
        /// <param name="eventService">The Event Service </param>
        /// <param name="cloudShim">The cloud api service</param>
        /// <param name="tenantApi">The tenant API.</param>
        public MachinesController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            ILoggerFactory loggerFactory,
            IEventService eventService,
            ICloudShim cloudShim,
            ITenantApi tenantApi)
        {
            _machineRepositoryAsyncLazy = new AsyncLazy<IRepository<Machine>>(() => repositoryFactory.CreateRepositoryAsync<Machine>());
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _cloudShim = cloudShim;
            _tenantApi = tenantApi;
        }

        public async Task<Machine> CreateMachineAsync(Machine machine, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<CreateMachineRequestValidator>(machine);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            machine.DateCreated = DateTime.UtcNow;
            machine.DateModified = DateTime.UtcNow;
            machine.Id = Guid.NewGuid();
            machine.NormalizedLocation = machine.Location.ToUpperInvariant();
            machine.MachineKey = machine.MachineKey.ToUpperInvariant();

            var result = await CreateMachineInDbAsync(machine, cancellationToken);

            _eventService.Publish(EventNames.MachineCreated, result);

            await _cloudShim.CopyMachineSettings(result.Id);

            return result;
        }

        public async Task<Machine> GetMachineByIdAsync(Guid machineId, Guid? tenantId, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<MachineIdValidator>(machineId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var machineRepository = await _machineRepositoryAsyncLazy;
            var batchOptions = tenantId == null
                ? new BatchOptions { EnableCrossPartitionQuery = true }
                : new BatchOptions { PartitionKey = new PartitionKey(tenantId.Value) };

            var result = await machineRepository.CreateItemQuery(batchOptions)
                .FirstOrDefaultAsync(m => m.Id == machineId, cancellationToken);

            return result ?? throw new NotFoundException($"A Machine resource could not be found for id {machineId}");
        }

        public async Task<Machine> GetMachineByKeyAsync(string machineKey, Guid? tenantId, CancellationToken cancellationToken)
        {
            var normalizedMachineKey = machineKey.ToUpperInvariant();
            var machineRepository = await _machineRepositoryAsyncLazy;
            var batchOptions = tenantId == null
                ? new BatchOptions { EnableCrossPartitionQuery = true }
                : new BatchOptions { PartitionKey = new PartitionKey(tenantId) };

            var machine = await machineRepository.CreateItemQuery(batchOptions)
                .FirstOrDefaultAsync(m => m.MachineKey.Equals(normalizedMachineKey), cancellationToken);

            return machine ?? throw new NotFoundException($"A Machine resource could not be found for id {machineKey}");
        }

        public async Task<Guid> GetMachineTenantIdAsync(Guid machineId, Guid? tenantId, CancellationToken cancellationToken)
        {
            var machineRepository = await _machineRepositoryAsyncLazy;
            var batchOptions = tenantId == null
                ? new BatchOptions { EnableCrossPartitionQuery = true }
                : new BatchOptions { PartitionKey = new PartitionKey(tenantId) };

            return await machineRepository.CreateItemQuery(batchOptions)
                .Where(m => m.Id == machineId)
                .Select(m => m.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Machine> UpdateMachineAsync(Machine model, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<UpdateMachineRequestValidator>(model);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            model.MachineKey = model.MachineKey.ToUpperInvariant();
            model.NormalizedLocation = model.Location.ToUpperInvariant();

            return await UpdateMachineInDbAsync(model);
        }

        public async Task DeleteMachineAsync(Guid machineId, Guid tenantId, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<MachineIdValidator>(machineId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var machineRepository = await _machineRepositoryAsyncLazy;
            try
            {
                await machineRepository.DeleteItemAsync(machineId, new QueryOptions { PartitionKey = new PartitionKey(tenantId) }, cancellationToken);
            }
            catch (DocumentNotFoundException)
            {
                // Suppressing this exception for deletes
            }
        }

        public async Task<Machine> ChangeMachineTenantAsync(ChangeMachineTenantRequest request, Guid? sourceTenantId, CancellationToken cancellationToken)
        {
            // This operation should only be performed by superadmins. While permissions to this
            // operation should be gated by policy documents, we need to make sure that if someone
            // gets this far as a non-superadmin, we have additional checks.

            // We're also assuming here that the existingMachine parameter has been retrieved from
            // this controller using GetMachineByIdAsync.

            // First, let's validate the target tenant ID because this is a free-form text field in
            // the Admin.
            if (request.TenantId == Guid.Empty)
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(request.TenantId), "The TenantId must not be empty") });
            }

            var machineRepository = await _machineRepositoryAsyncLazy;
            var batchOptions = sourceTenantId == null
                ? new BatchOptions { EnableCrossPartitionQuery = true }
                : new BatchOptions { PartitionKey = new PartitionKey(sourceTenantId.Value) };

            var existingMachine = await machineRepository.CreateItemQuery(batchOptions)
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (existingMachine == null)
            {
                throw new NotFoundException($"Unable to find machine with identifier '{request.Id}'");
            }

            if (existingMachine.TenantId == request.TenantId)
            {
                // Nothing to change.
                return existingMachine;
            }

            // Validate the target tenant by asking the Tenant service.
            var tenantResponse = await _tenantApi.GetTenantByIdAsync(request.TenantId);
            if (!tenantResponse.IsSuccess())
            {
                if (tenantResponse.ResponseCode != HttpStatusCode.NotFound)
                {
                    _logger.Error($"Failed to query the target tenant with identifier '{request.TenantId}' while attempting to move the machine '{request.Id}' to that tenant: ({tenantResponse.ResponseCode}) {tenantResponse.ErrorResponse?.Message ?? tenantResponse.ReasonPhrase}");
                }

                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(request.TenantId), "The TenantId provided in the request is either invalid or cannot be validated") });
            }

            // If the user didn't specify a setting profile identifier, we will pick the first
            // valid setting profile from the target tenant.
            var validateSettingProfileId = true;
            if (request.SettingProfileId == Guid.Empty)
            {
                var settingProfileIdsResponse = await _cloudShim.GetSettingProfileIdsForTenant(request.TenantId);
                if (!settingProfileIdsResponse.IsSuccess() || !settingProfileIdsResponse.Payload.Any())
                {
                    throw new ValidationFailedException(new[] { new ValidationFailure(nameof(request.SettingProfileId), "Unable to find a valid setting profile for the machine in the new tenant") });
                }

                request.SettingProfileId = settingProfileIdsResponse.Payload.First();

                // We don't need to validate a setting profile that we just obtained.
                validateSettingProfileId = false;
            }

            if (validateSettingProfileId && !await IsValidSettingProfileAsync(request.TenantId, request.SettingProfileId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(request.SettingProfileId), "The setting profile identifier is invalid or could not be validated") });
            }

            existingMachine.TenantId = request.TenantId;
            existingMachine.SettingProfileId = request.SettingProfileId;

            return await machineRepository.UpdateItemAsync(request.Id, existingMachine, cancellationToken: cancellationToken);
        }

        public async Task<List<Machine>> GetTenantMachinesAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var machineRepository = await _machineRepositoryAsyncLazy;
            return await machineRepository.CreateItemQuery()
                .Where(m => m.TenantId == tenantId)
                .ToListAsync(cancellationToken);
        }

        private async Task<Machine> CreateMachineInDbAsync(Machine machine, CancellationToken cancellationToken)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueLocationAsync(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Location), "Location is not unique"));
            }

            if (!await IsUniqueMachineKeyAsync(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.MachineKey), "Machine Key is not unique"));
            }

            if (validationErrors.Any())
            {
                throw new ValidationFailedException(validationErrors);
            }

            var machineRepository = await _machineRepositoryAsyncLazy;
            return await machineRepository.CreateItemAsync(machine, cancellationToken);
        }

        private async Task<Machine> UpdateMachineInDbAsync(Machine machine)
        {
            var validationErrors = new List<ValidationFailure>();

            if (machine.Id == Guid.Empty)
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.Id), "Machine identifier is required"));
            }

            var machineRepository = await _machineRepositoryAsyncLazy;
            var existingMachine = await machineRepository.GetItemAsync(machine.Id, new QueryOptions { PartitionKey = new PartitionKey(machine.TenantId) });

            if (existingMachine == null)
            {
                throw new NotFoundException($"No machine with identifier {machine.Id} was found");
            }

            if (!await IsUniqueLocationAsync(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.NormalizedLocation), "Location is not unique"));
            }

            if (!await IsUniqueMachineKeyAsync(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.MachineKey), "Machine key is not unique"));
            }

            if (validationErrors.Any())
            {
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

            return await machineRepository.UpdateItemAsync(machine.Id, existingMachine);
        }

        private async Task<bool> IsUniqueLocationAsync(Machine machine)
        {
            var machineRepository = await _machineRepositoryAsyncLazy;
            return !await machineRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(machine.TenantId) })
                .AnyAsync(m => m.NormalizedLocation == machine.NormalizedLocation && m.Id != machine.Id);
        }

        private async Task<bool> IsUniqueMachineKeyAsync(Machine machine)
        {
            var machineRepository = await _machineRepositoryAsyncLazy;
            return !await machineRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(machine.TenantId) })
                .AnyAsync(m => m.MachineKey == machine.MachineKey && m.Id != machine.Id);
        }

        private async Task<bool> IsValidSettingProfileAsync(Guid tenantId, Guid settingProfileId)
        {
            var response = await _cloudShim.ValidateSettingProfileId(tenantId, settingProfileId);
            if (response.IsSuccess())
            {
                return response.Payload;
            }

            _logger.Warning($"Unable to determine validity of setting profile ({settingProfileId}) in tenant ({tenantId}). Assuming it's not valid.\n({response.ResponseCode}) {response.ErrorResponse?.Message ?? response.ReasonPhrase}");
            return false;
        }
    }
}