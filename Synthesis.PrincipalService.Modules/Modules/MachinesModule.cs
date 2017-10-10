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
        private const string UserIdClaim = "UserId";
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
            SetupRoute_GetMachineById();

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

        private void SetupRoute_GetMachineById()
        {
            const string path = "/v1/machines/{id:guid}";
            Get(path, GetMachineByIdAsync, null, "GetMachineById");
            Get("/api/" + path, GetMachineByIdAsync, null, "GetMachineByIdLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = _serializer.Serialize(new Machine());
            var metadataDescription = "Retrieves a machine by Machine Id";

            _metadataRegistry.SetRouteMetadata("GetMachineById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetMachineByIdLegacy", new SynthesisRouteMetadata
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

        private async Task<object> GetMachineByIdAsync(dynamic input)
        {
            var machineId = input.id;
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                return await _machineController.GetMachineByIdAsync(machineId, tenantId);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (UnauthorizedAccessException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetMachineById: No access to get machines!");
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }
    }
}
