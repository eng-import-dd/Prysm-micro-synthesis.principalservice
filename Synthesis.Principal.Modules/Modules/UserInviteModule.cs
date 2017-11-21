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
using Synthesis.Authentication;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UserInviteModule : SynthesisModule
    {
        private readonly IUserInvitesController _userInviteController;

        public UserInviteModule(
            IUserInvitesController userInvitesController,
            IMetadataRegistry metadataRegistry,
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(PrincipalServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            _userInviteController = userInvitesController;

            this.RequiresAuthentication();

            CreateRoute("CreateUserInviteListForAccount", HttpMethod.Post, "/v1/userinvites", CreateUserInviteListForTenantAsync)
                .Description("Email invites for passed user list")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new UserInviteRequest())
                .ResponseFormat(new List<UserInviteResponse>{ new UserInviteResponse() });

            CreateRoute("ResendEmailInvitation", HttpMethod.Post, "/v1/userinvites/resend", ResendEmailInvitationAsync)
                .Description("Resend Email invites for passed user list")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new List<UserInviteRequest> { new UserInviteRequest() })
                .ResponseFormat(new List<UserInviteResponse> { new UserInviteResponse() });

            CreateRoute("GetdUsersInviteForTenantAsync", HttpMethod.Get, "/v1/userinvites", GetUsersInvitedForTenantAsync)
                .Description("Gets all invited users for Tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(new bool())
                .ResponseFormat(new PagingMetadata<UserInviteResponse> { List = new List<UserInviteResponse>() });
        }

        private async Task<object> CreateUserInviteListForTenantAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            List<UserInviteRequest> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInviteRequest>>();
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

        private async Task<object> ResendEmailInvitationAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            List<UserInviteRequest> invitedUsersList;
            try
            {
                invitedUsersList = this.Bind<List<UserInviteRequest>>();
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

            bool allUsers = input.allusers;
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
                Logger.Error("Failed to get users invited for Tenant due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

    }
}
