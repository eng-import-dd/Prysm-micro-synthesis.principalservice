using System;
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
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;

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

        private async Task<object> CreateGroupAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

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

            try
            {
                var result = await _groupsController.CreateGroupAsync(newGroup, TenantId, PrincipalId);

                return Negotiate.WithModel(result).WithStatusCode(HttpStatusCode.Created);
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

        private async Task<object> GetGroupByIdAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid groupId = input.id;
            try
            {
                return await _groupsController.GetGroupByIdAsync(groupId, TenantId);
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

        private async Task<object> GetGroupsForTenantAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                var result = await _groupsController.GetGroupsForTenantAsync(TenantId, PrincipalId);

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

        private async Task<object> DeleteGroupAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                Guid groupId = input.id;
                await _groupsController.DeleteGroupAsync(groupId, PrincipalId);

                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Resource has been deleted"
                };
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException ex)
            {
                Logger.Error("Group is either deleted or doesn't exist", ex);
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

        private async Task<object> UpdateGroupAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Group existingGroup;
            try
            {
                existingGroup = this.Bind<Group>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a Group resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                if (existingGroup.TenantId.Equals(Guid.Empty))
                {
                    existingGroup.TenantId = TenantId;
                }

                var result = await _groupsController.UpdateGroupAsync(existingGroup, TenantId, PrincipalId);

                return Negotiate.WithModel(result).WithStatusCode(HttpStatusCode.OK);
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