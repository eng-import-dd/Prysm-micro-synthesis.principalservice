using Nancy;
using Nancy.Json;
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

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UserInviteModule : NancyModule
    {
        private const string TenantIdClaim = "TenantId";
        private readonly IUserInvitesController _userInviteController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;

        public UserInviteModule(
            IUserInvitesController userInvitesController,
            IMetadataRegistry metadataRegistry,
            ILogger logger
            )
        {
            _metadataRegistry = metadataRegistry;
            _userInviteController = userInvitesController;
            _logger = logger;

            this.RequiresAuthentication();

            SetupRouteMetadata();

            Post("/v1/userinvites", CreateUserInviteListForAccountAsync, null, "CreateUserInviteListForAccount");
            Post("api/v1/userinvites", CreateUserInviteListForAccountAsync, null, "CreateUserInviteListForAccountLegacy");

            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("CreateUserInviteListForAccount", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Email Invite",
                Description = "Email invites for passed user list"
            });
        }

        private async Task<Object> CreateUserInviteListForAccountAsync(dynamic input)
        {
            List<UserInviteRequest> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInviteRequest>>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a User resource", ex);
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
                _logger.Error("Failed to resend an invite due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }

        }

    }
}
