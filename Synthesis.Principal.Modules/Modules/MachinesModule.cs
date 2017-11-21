using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Authentication;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class MachinesModule : SynthesisModule
    {
        private const string TenantIdClaim = "TenantId";
        private readonly IMachineController _machineController;

        public MachinesModule(
            IMachineController machineController,
            IMetadataRegistry metadataRegistry,
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(PrincipalServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            _machineController = machineController;

            this.RequiresAuthentication();

            CreateRoute("CreateMachine", HttpMethod.Post, "/v1/machines", CreateMachineAsync)
                .Description("Create a new machine resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new Machine())
                .ResponseFormat(new Machine());

            CreateRoute("GetMachineById", HttpMethod.Get, "/v1/machines/{id:guid}", GetMachineByIdAsync)
                .Description("Gets a machine by its unique identifier")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(new Machine());

            CreateRoute("UpdateMachine", HttpMethod.Put, "/v1/machines/{id:guid}", UpdateMachineAsync)
                .Description("Update a Principal resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .RequestFormat(new UpdateMachineRequest())
                .ResponseFormat(new MachineResponse());

            CreateRoute("DeleteMachine", HttpMethod.Delete, "/v1/machines/{id:guid}", DeleteMachineAsync)
                .Description("Deletes a machine")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("ChangeMachineAccount", HttpMethod.Put, "/v1/machines/{id:guid}/changeaccount", ChangeMachineAccountAsync)
                .Description("Changes a machine's account")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .RequestFormat(new UpdateMachineRequest())
                .ResponseFormat(new MachineResponse());

            CreateRoute("GetTenantMachines", HttpMethod.Get, "/v1/tenantmachines", GetTenantMachinesAsync)
                .Description("Retrieves a list of machines for a given tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .RequestFormat(string.Empty)
                .ResponseFormat(new List<MachineResponse>());
        }

        private async Task<object> CreateMachineAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            CreateMachineRequest newMachine;
            try
            {
                newMachine = this.Bind<CreateMachineRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a Machine resource", ex);
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
                Logger.Error("Failed to create machine resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateMachine);
            }
        }

        private async Task<object> GetMachineByIdAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

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
                Logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> UpdateMachineAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            UpdateMachineRequest updateMachine;

            try
            {
                updateMachine = this.Bind<UpdateMachineRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a Machine resource", ex);
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
                Logger.Error("Unhandled exception encountered while attempting to update a Machine resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> DeleteMachineAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

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
                Logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> ChangeMachineAccountAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            UpdateMachineRequest updateMachine;
            try
            {
                updateMachine = this.Bind<UpdateMachineRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a Machine resource", ex);
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
                Logger.Error("Unhandled exception encountered while attempting to Change Machine account", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> GetTenantMachinesAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

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
                Logger.LogMessage(LogLevel.Error, "GetTenantMachinesAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }
    }
}