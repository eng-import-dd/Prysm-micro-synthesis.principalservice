using System;
using System.Net.Http;
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
using Synthesis.PrincipalService.Controllers.Interfaces;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Modules
{
    public class GroupsModule : SynthesisModule
    {
        private const string TenantIdClaim = "TenantId";
        private const string UserIdClaim = "UserId";
        private readonly IGroupsController _groupsController;

        public GroupsModule(
            IGroupsController groupsController,
            IMetadataRegistry metadataRegistry,
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(PrincipalServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            _groupsController = groupsController;

            //this.RequiresAuthentication();

            CreateRoute("CreateGroup", HttpMethod.Post, "/v1/groups", CreateGroupAsync)
                .Description("Creates a new group")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new Group())
                .ResponseFormat(new Principal());

            CreateRoute("GetGroupById", HttpMethod.Get, "/v1/groups/{id}", GetGroupByIdAsync)
                .Description("Get a group by its unique identifier")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("GetGroupsForTenant", HttpMethod.Get, "/v1/groups/tenant", GetGroupsForTenantAsync)
                .Description("Get Group for a tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("DeleteGroup", HttpMethod.Delete, "/v1/groups/{groupId}", DeleteGroupAsync)
                .Description("Deletes a Group")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("UpdateGroup", HttpMethod.Put, "/v1/groups", UpdateGroupAsync)
                .Description("Updates an existing Group")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
        }

        private async Task<object> CreateGroupAsync(dynamic input)
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

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);

                var result = await _groupsController.CreateGroupAsync(newGroup, tenantId, userId);

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
            Guid groupId = input.id;
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);

                return await _groupsController.GetGroupByIdAsync(groupId, tenantId);
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
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetGroupById: No valid account level access to groups!");
            }
            catch (Exception ex)
            {
                Logger.Error("GetGroupByIdAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetGroupsForTenantAsync(dynamic input)
        {
            //A list of the Groups which belong to the current user's tenant
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);

                var result = await _groupsController.GetGroupsForTenantAsync(tenantId, userId);

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
            try
            {
                Guid groupId = Guid.Parse(input.groupId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);
                await _groupsController.DeleteGroupAsync(groupId, userId);

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
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);

                if (existingGroup.TenantId.Equals(Guid.Empty))
                {
                    existingGroup.TenantId = tenantId;
                }

                var result = await _groupsController.UpdateGroupAsync(existingGroup, tenantId, userId);

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