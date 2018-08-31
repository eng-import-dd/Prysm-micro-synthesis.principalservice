using System;
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
    public class GroupsModule : SynthesisModule
    {
        private readonly IGroupsController _groupsController;

        public GroupsModule(
            IGroupsController groupsController,
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(ServiceInformation.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _groupsController = groupsController;

            this.RequiresAuthentication();

            CreateRoute("CreateGroup", HttpMethod.Post, Routing.Groups, CreateGroupAsync)
                .Description("Creates a new group")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(Group.Example())
                .ResponseFormat(Group.Example());

            CreateRoute("GetGroupById", HttpMethod.Get, Routing.GroupsWithId, GetGroupByIdAsync)
                .Description("Get a group by its unique identifier")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(Group.Example());

            CreateRoute("GetGroupsForTenant", HttpMethod.Get, Routing.Groups, GetGroupsForTenantAsync)
                .Description("Get Group for a tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(Group.Example());

            CreateRoute("DeleteGroup", HttpMethod.Delete, Routing.GroupsWithId, DeleteGroupAsync)
                .Description("Deletes a Group")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("UpdateGroup", HttpMethod.Put, Routing.Groups, UpdateGroupAsync)
                .Description("Updates an existing Group")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(Group.Example())
                .ResponseFormat(Group.Example());
        }

        private async Task<object> CreateGroupAsync(dynamic input, CancellationToken cancellationToken)
        {
            Group newGroup;
            try
            {
                newGroup = this.Bind<Group>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a Group resource", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .WithTenantIdExpansion(ctx => TenantId)
                .ExecuteAsync(cancellationToken);

            try
            {
                // Force the group type to Custom because you can't create a built-in group using
                // this route.
                newGroup.Type = GroupType.Custom;

                var result = await _groupsController.CreateGroupAsync(newGroup, TenantId, PrincipalId, cancellationToken);

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
                Logger.Error("Failed to create group resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetGroupByIdAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid groupId = input.id;
            try
            {
                var result = await _groupsController.GetGroupByIdAsync(groupId, TenantId, cancellationToken);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => result.TenantId.GetValueOrDefault())
                    .ExecuteAsync(cancellationToken);

                return result;
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (UnauthorizedAccessException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetGroupById: No valid tenant level access to groups!");
            }
            catch (Exception ex)
            {
                Logger.Error("GetGroupByIdAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetGroupsForTenantAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .WithTenantIdExpansion(ctx => TenantId)
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _groupsController.GetGroupsForTenantAsync(TenantId, cancellationToken);

                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetGroupsForTenantAsync: No valid tenant level access to groups!");
            }
            catch (Exception ex)
            {
                Logger.Error("GetGroupsForTenantAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> DeleteGroupAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid groupId = input.id;

            await RequiresAccess()
                .WithTenantIdExpansion(async (ctx, ct) => (await _groupsController.GetTenantIdForGroupIdAsync(groupId, TenantId, ct)).GetValueOrDefault())
                .ExecuteAsync(cancellationToken);

            try
            {
                await _groupsController.DeleteGroupAsync(groupId, TenantId, PrincipalId, cancellationToken);

                return Response.NoContent("Resource has been deleted");
            }
            catch (ValidationFailedException ex)
            {
                Logger.Info($"Validation failed while deleting group '{groupId}' from tenant '{TenantId}' as principal '{PrincipalId}'", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException ex)
            {
                Logger.Info("Group is either deleted or doesn't exist", ex);
                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Group is either deleted or doesn't exist"
                };
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create group resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> UpdateGroupAsync(dynamic input, CancellationToken cancellationToken)
        {
            Group group;
            try
            {
                group = this.Bind<Group>();
            }
            catch (Exception ex)
            {
                return Response.BadRequestBindingException(ex.Message);
            }

            try
            {
                var result = await _groupsController.UpdateGroupAsync(group, PrincipalId, cancellationToken);

                await RequiresAccess()
                    .WithTenantIdExpansion(ctx => result.TenantId.GetValueOrDefault())
                    .ExecuteAsync(cancellationToken);

                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundGroup);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update group resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }
    }
}