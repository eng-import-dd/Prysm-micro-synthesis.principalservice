using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Models;
using UserInvite = Synthesis.PrincipalService.InternalApi.Models.UserInvite;
using Synthesis.PrincipalService.InternalApi.Constants;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UserInviteModule : SynthesisModule
    {
        private readonly IUserInvitesController _userInviteController;

        public UserInviteModule(
            IUserInvitesController userInvitesController,
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(ServiceInformation.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _userInviteController = userInvitesController;

            this.RequiresAuthentication();

            CreateRoute("CreateUserInviteListForTenant", HttpMethod.Post, Routing.UserInvites, _ => CreateUserInviteListForTenantAsync())
                .Description("Email invites for passed user list")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new List<UserInvite> { UserInvite.Example() })
                .ResponseFormat(new List<UserInvite> { UserInvite.Example() });

            CreateRoute("ResendEmailInvitation", HttpMethod.Post, Routing.ResendUserInvites, _ => ResendEmailInvitationAsync())
                .Description("Resend Email invites for passed user list")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new List<UserInvite> { UserInvite.Example() })
                .ResponseFormat(new List<UserInvite> { UserInvite.Example() });

            CreateRoute("GetUserInvitesForTenantAsync", HttpMethod.Get, Routing.UserInvites, GetUsersInvitedForTenantAsync)
                .Description("Gets all invited users for Tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new PagingMetadata<UserInvite> { List = new List<UserInvite> { UserInvite.Example() } });
        }

        private async Task<object> CreateUserInviteListForTenantAsync()
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            List<UserInvite> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInvite>>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting send user invite", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userInviteController.CreateUserInviteListAsync(invitedUsersList, TenantId);
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
                Logger.Error("Failed to send an invite due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> ResendEmailInvitationAsync()
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            List<UserInvite> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInvite>>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to resend user invites", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userInviteController.ResendEmailInviteAsync(invitedUsersList, TenantId);
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
                Logger.Error("Failed to resend an invite due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetUsersInvitedForTenantAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            bool.TryParse(Request.Query["allusers"], out bool allUsers);

            try
            {
                var result = await _userInviteController.GetUsersInvitedForTenantAsync(TenantId, allUsers);
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
            catch (Exception ex)
            {
                Logger.Error($"Failed to get users invited for tenant {TenantId} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }
    }
}