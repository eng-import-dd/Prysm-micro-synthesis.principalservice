using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers.Interfaces;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class MachinesModule : SynthesisModule
    {
        private readonly IMachineController _machineController;

        public MachinesModule(
            IMachineController machineController,
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(PrincipalServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _machineController = machineController;

            this.RequiresAuthentication();

            CreateRoute("CreateMachine", HttpMethod.Post, "/v1/machines", CreateMachineAsync)
                .Description("Create a new machine resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(Machine.Example())
                .ResponseFormat(Machine.Example());

            CreateRoute("GetMachineById", HttpMethod.Get, "/v1/machines/{id:guid}", GetMachineByIdAsync)
                .Description("Gets a machine by its unique identifier")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(Machine.Example());

            CreateRoute("GetMachineByKey", HttpMethod.Get, "/v1/machines", GetMachineByKeyAsync, c => c.Request.Query.ContainsKey("machinekey"))
                .Description("Get a machine by machine key")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(Machine.Example());

            CreateRoute("UpdateMachine", HttpMethod.Put, "/v1/machines/{id:guid}", UpdateMachineAsync)
                .Description("Update a Principal resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(Machine.Example())
                .ResponseFormat(Machine.Example());

            CreateRoute("DeleteMachine", HttpMethod.Delete, "/v1/machines/{id:guid}", DeleteMachineAsync)
                .Description("Deletes a machine")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("ChangeMachineTenant", HttpMethod.Put, "/v1/machines/{id:guid}/changetenant", ChangeMachineTenantAsync)
                .Description("Changes a machine's tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(Machine.Example())
                .ResponseFormat(Machine.Example());

            CreateRoute("GetTenantMachines", HttpMethod.Get, "/v1/machines", GetTenantMachinesAsync, c => !c.Request.Query.ContainsKey("machinekey"))
                .Description("Retrieves a list of machines for the tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<Machine> { Machine.Example() });
        }

        private async Task<object> CreateMachineAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Machine newMachine;
            try
            {
                newMachine = this.Bind<Machine>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _machineController.CreateMachineAsync(newMachine, TenantId);

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
                Logger.Error("Failed to create machine resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateMachine);
            }
        }

        private async Task<object> GetMachineByIdAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            var machineId = input.id;
            try
            {
                return await _machineController.GetMachineByIdAsync(machineId, TenantId, IsServicePrincipal);
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
                Logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> GetMachineByKeyAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            var machinekey = Request.Query.machinekey;
            try
            {
                return await _machineController.GetMachineByKeyAsync(machinekey, TenantId, IsServicePrincipal);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetMachineByKey: No access to get machines!");
            }
            catch (Exception ex)
            {
                Logger.Error("GetMachineByKey threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> UpdateMachineAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Machine updateMachine;

            try
            {
                updateMachine = this.Bind<Machine>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _machineController.UpdateMachineAsync(updateMachine, TenantId, IsServicePrincipal);
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
                Logger.Error("Unhandled exception encountered while attempting to update a Machine resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> DeleteMachineAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            var machineId = input.id;

            try
            {
                await _machineController.DeleteMachineAsync(machineId, TenantId);

                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Machine has been deleted"
                };
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
                Logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> ChangeMachineTenantAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Machine updateMachine;
            try
            {
                updateMachine = this.Bind<Machine>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _machineController.ChangeMachineTenantasync(updateMachine.Id, TenantId, updateMachine.SettingProfileId.Value);
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
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "ChangeMachineTenant: Not authorized.");
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to Change Machine tenant", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> GetTenantMachinesAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _machineController.GetTenantMachinesAsync(TenantId);
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
                Logger.LogMessage(LogLevel.Error, "GetTenantMachinesAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }
    }
}