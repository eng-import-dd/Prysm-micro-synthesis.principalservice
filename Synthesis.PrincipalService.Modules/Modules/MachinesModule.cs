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
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class MachinesModule : AbstractModule
    {
        private const string TenantIdClaim = "TenantId";
        private const string UserIdClaim = "UserId";
        private readonly IMachineController _machineController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;
        private const string DeprecationWarning = "DEPRECATED";

        public MachinesModule(
            IMetadataRegistry metadataRegistry,
            IMachineController machineController,
            ILoggerFactory loggerFactory)
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _machineController = machineController;
            _logger = loggerFactory.GetLogger(this);

            this.RequiresAuthentication();

            // Initialize documentation
            SetupRouteMetadata();
            SetupRoute_GetMachineById();
            SetupRoute_GetMachineByKey();
            SetupRoute_UpdateMachine();
            SetupRoute_DeleteMachine();
            SetupRoute_ChangeMachineAccount();
            SetupRoute_GetTenantMachines();

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
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new Machine());
            var metadataDescription = "Retrieves a machine by Machine Id";

            _metadataRegistry.SetRouteMetadata("GetMachineById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetMachineByIdLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetMachineByKey()
        {
            const string path = "/v1/machines/machinekey/{machineKey}";
            Get(path, GetMachineByKeyAsync, null, "GetMachineByKey");
            Get("/api/" + path, GetMachineByKeyAsync, null, "GetMachineByKeyLegacy");

            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new Machine());
            var metadataDescription = "Retrieves a machine by Machine Key";

            _metadataRegistry.SetRouteMetadata("GetMachineByKey", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetMachineByKeyLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_UpdateMachine()
        {
            const string path = "/v1/machines/{id:guid}";
            Put(path, UpdateMachineAsync, null, "UpdateMachineAsync");
            Put("/api/" + path, UpdateMachineAsync, null, "UpdateMachineAsyncLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new UpdateMachineRequest());
            var metadataResponse = ToFormattedJson(new MachineResponse());
            var metadataDescription = "Updates a machine";

            _metadataRegistry.SetRouteMetadata("UpdateMachine", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("UpdateMachineLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_DeleteMachine()
        {
            const string path = "/v1/machines/{id:guid}";
            Delete(path, DeleteMachineAsync, null, "DeleteMachineAsync");
            Delete("/api/" + path, DeleteMachineAsync, null, "DeleteMachineAsyncLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new Machine());
            var metadataDescription = "Deletes a machine";

            _metadataRegistry.SetRouteMetadata("DeleteMachine", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("DeleteMachineLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_ChangeMachineAccount()
        {
            const string path = "/v1/machines/{id:guid}/changeaccount";
            Put(path, ChangeMachineAccountAsync, null, "ChangeMachineAccountAsync");
            Put("/api/" + path, ChangeMachineAccountAsync, null, "ChangeMachineAccountAsyncLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new UpdateMachineRequest());
            var metadataResponse = ToFormattedJson(new MachineResponse());
            var metadataDescription = "Changes the machine's account";

            _metadataRegistry.SetRouteMetadata("ChangeMachineAccount", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("ChangeMachineAccountLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetTenantMachines()
        {
            const string path = "/v1/tenantmachines";
            Get(path, GetTenantMachinesAsync, null, "GetTenantMachines");
            Get("/api" + path, GetTenantMachinesAsync, null, "GetTenantMachinesLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(string.Empty);
            var metadataResponse = ToFormattedJson(new List<MachineResponse>());
            var metadataDescription = "Retrieves list of machines for a tenant";

            _metadataRegistry.SetRouteMetadata("GetMachineById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetTenantMachinesLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
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
                _logger.Error("Binding failed while attempting to create a Machine resource", ex);
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
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateMachine);
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
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetMachineById: No access to get machines!");
            }
            catch (Exception ex)
            {
                _logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> GetMachineByKeyAsync(dynamic input)
        {
            var machineKey = input.machineKey;
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                return await _machineController.GetMachineByKeyAsync(machineKey, tenantId);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetMachineByKey: No access to get machines!");
            }
            catch (Exception ex)
            {
                _logger.Error("GetMachineByKey threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
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
                _logger.Error("Binding failed while attempting to update a Machine resource", ex);
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
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "UpdateMachine: Not authorized to edit this machine!");
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to update a Machine resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> DeleteMachineAsync(dynamic input)
        {
            var machineId = input.id;

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                await _machineController.DeleteMachineAsync(machineId, tenantId);

                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Machine has been deleted"
                };
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetMachineById: No access to get machines!");
            }
            catch (Exception ex)
            {
                _logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> ChangeMachineAccountAsync(dynamic input)
        {
            UpdateMachineRequest updateMachine;
            try
            {
                updateMachine = this.Bind<UpdateMachineRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to update a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);

            try
            {
                return await _machineController.ChangeMachineAccountAsync(updateMachine.Id, tenantId, updateMachine.SettingProfileId);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "ChangeMachineAccount: Not authorized.");
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to Change Machine account", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> GetTenantMachinesAsync(dynamic input)
        {
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                return await _machineController.GetTenantMachinesAsync(tenantId);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachines);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "GetTenantMachinesAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }
    }
}
