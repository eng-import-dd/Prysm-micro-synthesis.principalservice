using AutoMapper;
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
        private readonly IValidator _machineIdValidator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly ILicenseApi _licenseApi;
        private readonly IEmailUtility _emailUtility;
        private readonly IMapper _mapper;
        private readonly string _deploymentType;
        private const string OrgAdminRoleName = "Org_Admin";
        private const string BasicUserRoleName = "Basic_User";

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

            return _mapper.Map<Machine, MachineResponse>(result);
        }

        private async Task<Machine> CreateMachineInDB(Machine machine)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueLocation(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.MachineId), "Location was not unique"));
            }

            if (!await IsUniqueMachineKey(machine))
            {
                validationErrors.Add(new ValidationFailure(nameof(machine.MachineId), "Machine Key was not unique"));
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
            var accountMachines = await _machineRepository.GetItemsAsync(m => m.AccountId == machine.AccountId);
            return accountMachines.Any(x => x.Location == machine.Location && x.MachineId == machine.MachineId);
        }

        private async Task<bool> IsUniqueMachineKey(Machine machine)
        {
            var machinesWithMatchingMachinesKey = await _machineRepository.GetItemsAsync(m => m.MachineKey == machine.MachineKey);
            return machinesWithMatchingMachinesKey.Any(x => x.MachineId != machine.MachineId) == false;
        }

        //private async Task<bool> CopyMachineSettings(Guid machineId, Guid fromMachineId)
        //{
        //    if(machineId == Guid.Empty)
        //    {
        //        _logger.Error("MachineId cannot be empty");
        //        return false;
        //    }

        //    // TODO: The cloud implementation involves an SP. Need to figure out the correct way to implement it here.
        //    var result = await _machineRepository.CreateItemAsync();
        //    return true;
        //}
    }
}
