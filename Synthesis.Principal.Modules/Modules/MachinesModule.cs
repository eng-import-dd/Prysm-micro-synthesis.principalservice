using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ErrorHandling;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
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
            : base(ServiceInformation.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _machineController = machineController;

            this.RequiresAuthentication();

            CreateRoute("CreateMachine", HttpMethod.Post, Routing.Machines, CreateMachineAsync)
                .Description("Create a new machine resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(Machine.Example())
                .ResponseFormat(Machine.Example());

            CreateRoute("GetMachineById", HttpMethod.Get, Routing.MachinesWithId, GetMachineByIdAsync)
                .Description("Gets a machine by its unique identifier")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(Machine.Example());

            CreateRoute("GetMachineByKey", HttpMethod.Get, Routing.Machines, GetMachineByKeyAsync, c => c.Request.Query.ContainsKey("machinekey"))
                .Description("Get a machine by machine key")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(Machine.Example());

            CreateRoute("UpdateMachine", HttpMethod.Put, Routing.MachinesWithId, UpdateMachineAsync)
                .Description("Update a Principal resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(Machine.Example())
                .ResponseFormat(Machine.Example());

            CreateRoute("DeleteMachine", HttpMethod.Delete, Routing.MachinesWithId, DeleteMachineAsync)
                .Description("Deletes a machine")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("ChangeMachineTenant", HttpMethod.Put, Routing.ChangeMachineTenant, ChangeMachineTenantAsync)
                .Description("Changes a machine's tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(Machine.Example())
                .ResponseFormat(Machine.Example());

            CreateRoute("GetTenantMachines", HttpMethod.Get, Routing.Machines, GetTenantMachinesAsync, c => !c.Request.Query.ContainsKey("machinekey"))
                .Description("Retrieves a list of machines for the tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<Machine> { Machine.Example() });
        }

        private async Task<object> CreateMachineAsync(dynamic input)
        {

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

            await RequiresAccess()
                .WithTenantIdExpansion(ctx => newMachine.TenantId)
                .ExecuteAsync(CancellationToken.None);

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
                Logger.Error("Failed to create machine resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateMachine);
            }
        }

        private async Task<object> GetMachineByIdAsync(dynamic input)
        {
            var machineId = input.id;

            try
            {
                var machine = await _machineController.GetMachineByIdAsync(machineId);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => machine.TenantId)
                    .ExecuteAsync(CancellationToken.None);

                return machine;
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
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
            var machinekey = Request.Query.machinekey;

            try
            {
                var machine = await _machineController.GetMachineByKeyAsync(machinekey);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => machine.TenantId)
                    .ExecuteAsync(CancellationToken.None);

                return machine;
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
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

            await RequiresAccess()
                .WithTenantIdExpansion(_ => updateMachine.TenantId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _machineController.UpdateMachineAsync(updateMachine);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a Machine resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> DeleteMachineAsync(dynamic input)
        {
            Guid machineId = input.id;

            try
            {
                var machine = await _machineController.GetMachineByIdAsync(machineId);

                await RequiresAccess()
                    .WithTenantIdExpansion(context => machine.TenantId)
                    .ExecuteAsync(CancellationToken.None);

                await _machineController.DeleteMachineAsync(machineId);

                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Machine has been deleted"
                };
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
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