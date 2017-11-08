using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Workflow.Controllers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UserInviteModule : AbstractModule
    {
        private const string TenantIdClaim = "TenantId";
        private readonly IUserInvitesController _userInviteController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;

        public UserInviteModule(
            IUserInvitesController userInvitesController,
            IMetadataRegistry metadataRegistry,
            ILoggerFactory loggerFactory
            )
        {
            _metadataRegistry = metadataRegistry;
            _userInviteController = userInvitesController;
            _logger = loggerFactory.GetLogger(this);

            this.RequiresAuthentication();

            SetupRouteMetadata();

            Post("/v1/userinvites", CreateUserInviteListForTenantAsync, null, "CreateUserInviteListForAccount");
            Post("api/v1/userinvites", CreateUserInviteListForTenantAsync, null, "CreateUserInviteListForAccountLegacy");
            Post("/v1/userinvites/resend", ResendEmailInvitationAsync, null, "ResendEmailInvitation");
            Post("api/v1/userinvites/resend", ResendEmailInvitationAsync, null, "ResendEmailInvitationLegacy");

            Get("/v1/userinvites", GetUsersInvitedForTenantAsync, null, "GetdUsersInviteForTenantAsync");
            Get("/api/v1/userinvites", GetUsersInvitedForTenantAsync, null, "GetUsersInvitedForTenantLegacy");


            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("CreateUserInviteListForTenant", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = ToFormattedJson(new UserInviteRequest()),
                Response = ToFormattedJson(new List<UserInviteResponse> { new UserInviteResponse() }),
                Description = "Email invites for passed user list"
            });

            _metadataRegistry.SetRouteMetadata("ResendEmailInvitation", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = ToFormattedJson(new List<UserInviteRequest> { new UserInviteRequest() }),
                Response = ToFormattedJson(new List<UserInviteResponse> { new UserInviteResponse() }),
                Description = "Resend Email invites for passed user list"
            });

            _metadataRegistry.SetRouteMetadata("GetInvitedUsersForTenant", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = ToFormattedJson(new bool()),
                Response = ToFormattedJson(new PagingMetadata<UserInviteResponse> { List = new List<UserInviteResponse>() }),
                Description = "Gets all invited users for Tenant"
            });
        }

        private async Task<Object> CreateUserInviteListForTenantAsync(dynamic input)
        {
            List<UserInviteRequest> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInviteRequest>>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting send user invite", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                var result = await _userInviteController.CreateUserInviteListAsync(invitedUsersList, tenantId);
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
                _logger.Error("Failed to send an invite due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }

        }

        private async Task<Object> ResendEmailInvitationAsync(dynamic input)
        {
            List<UserInviteRequest> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInviteRequest>>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to resend user invites", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                var result = await _userInviteController.ResendEmailInviteAsync(invitedUsersList, tenantId);
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
                _logger.Error("Failed to resend an invite due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetUsersInvitedForTenantAsync(dynamic input)
        {
            bool allUsers = input.allusers;
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                var result = await _userInviteController.GetUsersInvitedForTenantAsync(tenantId, allUsers);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get users invited for Tenant due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

    }
}
