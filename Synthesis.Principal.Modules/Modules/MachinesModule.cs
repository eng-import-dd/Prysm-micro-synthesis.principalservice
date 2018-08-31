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
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class MachinesModule : SynthesisModule
    {
        private readonly IMachinesController _machinesController;

        public MachinesModule(
            IMachinesController machineController,
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(ServiceInformation.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _machinesController = machineController;

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

        private Guid? NullableTenantId => IsServicePrincipal ? new Guid?() : TenantId;

        private async Task<object> CreateMachineAsync(dynamic input, CancellationToken cancellationToken)
        {
            Machine newMachine;
            try
            {
                newMachine = this.Bind<Machine>();
            }
            catch (Exception ex)
            {
                Logger.Info("Binding failed while attempting to create a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .WithTenantIdExpansion(c => newMachine.TenantId)
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _machinesController.CreateMachineAsync(newMachine, cancellationToken);

                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Debug("Validation failed for CreateMachine", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create machine resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateMachine);
            }
        }

        private async Task<object> GetMachineByIdAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid machineId = input.id;

            try
            {
                var machine = await _machinesController.GetMachineByIdAsync(machineId, NullableTenantId, cancellationToken);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => machine.TenantId)
                    .ExecuteAsync(cancellationToken);

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
                Logger.Debug("Validation failed for GetMachineById", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("GetMachineById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> GetMachineByKeyAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            string machinekey = Request.Query.machinekey;
            try
            {
                var machine = await _machinesController.GetMachineByKeyAsync(machinekey, NullableTenantId, cancellationToken);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => machine.TenantId)
                    .ExecuteAsync(cancellationToken);

                return machine;
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
            }
            catch (ValidationFailedException ex)
            {
                Logger.Debug("Validation failed for GetMachineByKey", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (Exception ex)
            {
                Logger.Error("GetMachineByKey threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> UpdateMachineAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

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
                .WithTenantIdExpansion(ctx => updateMachine.TenantId)
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _machinesController.UpdateMachineAsync(updateMachine, cancellationToken);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Debug("Validation failed for UpdateMachine", ex);
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

        private async Task<object> DeleteMachineAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid machineId = input.id;

            try
            {
                var tenantId = await _machinesController.GetMachineTenantIdAsync(machineId, NullableTenantId, cancellationToken);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => tenantId)
                    .ExecuteAsync(cancellationToken);

                await _machinesController.DeleteMachineAsync(machineId, tenantId, cancellationToken);

                return Response.NoContent("Machine has been deleted");
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
            }
            catch (ValidationFailedException ex)
            {
                Logger.Debug("Validation failed for DeleteMachine", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"An unhandled exception was encountered while deleting machine '{machineId}'", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }

        private async Task<object> ChangeMachineTenantAsync(dynamic input, CancellationToken cancellationToken)
        {
            ChangeMachineTenantRequest request;
            try
            {
                request = this.Bind<ChangeMachineTenantRequest>();
            }
            catch (Exception ex)
            {
                Logger.Info("Binding failed while attempting to update a Machine resource", ex);
                return Response.BadRequestBindingException();
            }

            // We need to make sure that this route is NOT in the default policy document.
            // Only the SuperAdmin group should have access to this route.

            // The permissions logic for this method is extremely messy because there are source
            // and target tenants involved. The user needs to have access to both of them. I think
            // that the "tenantAccess" condition needs to be expanded to include whether or not
            // the user can impersonate a particular tenant as well if the tenant claim on the
            // principal doesn't match the tenant of the resource in question.

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _machinesController.ChangeMachineTenantAsync(request, NullableTenantId, cancellationToken);
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
            }
            catch (ValidationFailedException ex)
            {
                Logger.Debug("Validation failed for ChangeMachineTenant", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundMachine);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to Change Machine Tenant", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateMachine);
            }
        }

        private async Task<object> GetTenantMachinesAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _machinesController.GetTenantMachinesAsync(TenantId, cancellationToken);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Debug("Validation failed for GetTenantMachines", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to Get Tenant Machines", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetMachine);
            }
        }
    }
}