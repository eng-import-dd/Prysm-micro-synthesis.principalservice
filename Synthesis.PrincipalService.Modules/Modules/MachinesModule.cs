using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Requests;


namespace Synthesis.PrincipalService.Modules
{
    public sealed class MachinesModule : NancyModule
    {
        //private const string TenantIdClaim = "TenantId";
        //private const string UserIdClaim = "UserId";
        private readonly IMachineController _machineController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;

        public MachinesModule(
            IMetadataRegistry metadataRegistry,
            IMachineController machineController,
            ILogger logger)
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _machineController = machineController;
            _logger = logger;

            this.RequiresAuthentication();

            // Initialize documentation
            SetupRouteMetadata();

            // Routes
            // CRUD routes
            Post("/v1/machines", CreateMachineAsync, null, "CreateMachine");
            Post("/api/v1/machines", CreateMachineAsync, null, "CreateMachineLegacy");

            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("CreateMachine", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Create a new machine",
                Description = ""
            });
        }

        private async Task<object> CreateMachineAsync(dynamic input)
        {
            CreateMachineRequest newMachine;
            try
            {
                newMachine = this.Bind<CreateMachineRequest>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a Machine resource", ex);
                return Response.BadRequestBindingException();
            }
            try
            {
                var result = await _machineController.CreateMachineAsync(newMachine);

                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create machine resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateMachine); ;
            }
        }
    }
}
