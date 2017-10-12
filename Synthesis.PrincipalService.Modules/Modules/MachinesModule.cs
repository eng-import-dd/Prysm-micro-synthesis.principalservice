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
using System.Web.Script.Serialization;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class MachinesModule : NancyModule
    {
        private const string TenantIdClaim = "TenantId";
        private readonly IMachineController _machineController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private const string DeprecationWarning = "DEPRECATED";

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
            SetupRoute_UpdateMachine();

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
                ValidStatusCodes = new[] { HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Create a new machine",
                Description = "Create a new machine resource."
            });
        }

        private void SetupRoute_UpdateMachine()
        {
            const string path = "/v1/machines/{id:guid}";
            Put(path, UpdateMachineAsync, null, "UpdateMachineAsync");
            Put("/api/" + path, UpdateMachineAsync, null, "UpdateMachineAsyncLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = _serializer.Serialize(new Machine());
            var metadataDescription = "Updates a machine";

            _metadataRegistry.SetRouteMetadata("UpdateMachine", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("UpdateMachineLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
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
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                var result = await _machineController.CreateMachineAsync(newMachine, tenantId);

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

        private async Task<object> UpdateMachineAsync(dynamic input)
        {
            Guid machineId;
            UpdateMachineRequest updateMachine;

            try
            {
                machineId = Guid.Parse(input.Id);
                updateMachine = this.Bind<UpdateMachineRequest>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                return await _machineController.UpdateMachineAsync(updateMachine, tenantId);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch(Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to update a Machine resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }
    }
}
