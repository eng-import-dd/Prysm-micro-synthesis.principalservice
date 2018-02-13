using FluentValidation;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.PrincipalService.Requests;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.Extensions;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UsersModule : SynthesisModule
    {
        private readonly IUsersController _userController;

        public UsersModule(
            IUsersController userController,
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(PrincipalServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _userController = userController;

            this.RequiresAuthentication();

            CreateRoute("CreateUser", HttpMethod.Post, "/v1/users", CreateUserAsync)
                .Description("Create a new User resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(CreateUserRequest.Example())
                .ResponseFormat(User.Example());

            CreateRoute("GetUsersForAccount", HttpMethod.Get, "/v1/users/", GetUsersForAccountAsync)
                .Description("Retrieve all Users resource")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new PagingMetadata<UserResponse> { List = new List<UserResponse> { UserResponse.Example() } });

            CreateRoute("UpdateUser", HttpMethod.Put, "/v1/users/{id:guid}", UpdateUserAsync)
                .Description("Update a User resource")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(UpdateUserRequest.Example())
                .ResponseFormat(UserResponse.Example());

            CreateRoute("LockUser", HttpMethod.Post, "/v1/users/{userId:guid}/lock", LockUserAsync)
                .Description("Locks the respective user")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(User.Example())
                .ResponseFormat(new bool());

            CreateRoute("CreateUserGroup", HttpMethod.Post, "/v1/usergroups", CreateUserGroupAsync)
                .Description("Creates a User Group")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(CreateUserGroupRequest.Example())
                .ResponseFormat(User.Example());

            CreateRoute("CanPromoteUser", HttpMethod.Get, "/v1/users/canpromoteuser/{0}", CanPromoteUserAsync)
                .Description("States whether a user can be promoted")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(CanPromoteUserResponse.Example());

            CreateRoute("GetGroupUsers", HttpMethod.Get, "/v1/groups/{id}/users", GetGroupUsersAsync)
                .Description("Retrieves user groups by group Id")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<Guid> { new Guid() });

            CreateRoute("GetUserGroupsForUser", HttpMethod.Get, "/v1/users/{userId}/groups", GetUserGroupsForUserAsync)
                .Description("Retrieves user groups by user Id")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<Guid> { new Guid() });

            CreateRoute("GetTenantIdByUserEmail", HttpMethod.Get, "/v1/users/tenantid/{email}", GetTenantIdByUserEmailAsync)
                .Description("Retrieves tenant id by user email")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new Guid());

            CreateRoute("RemoveUserFromPermissionGroup", HttpMethod.Delete, "v1/groups/{groupId}/users/{userId}", RemoveUserFromPermissionGroupAsync)
                .Description("Removes a specific user from the group")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("GetUserById", HttpMethod.Get, "/v1/users/{id:guid}", GetUserByIdAsync)
                .Description("Gets a User resource by UserId")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(UserResponse.Example());

            CreateRoute("GetUsersByIds", HttpMethod.Post, Routing.GetUsersByIds, GetUsersByIdsAsync)
                .Description("Get a Principal resource by it's identifier.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<User> { User.Example() });

            CreateRoute("GetUsersBasic", HttpMethod.Get, "/v1/users/basic", GetUsersBasicAsync)
                .Description("Retrieves a users basic details")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new PagingMetadata<BasicUserResponse> { List = new List<BasicUserResponse> { BasicUserResponse.Example() } });

            CreateRoute("GetUserByIdBasic", HttpMethod.Get, "/v1/users/{userId:guid}/basic", GetUserByIdBasicAsync)
                .Description("Get a Principal resource by it's identifier.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(UserResponse.Example());

            CreateRoute("GetUserByUserNameOrEmail", HttpMethod.Get, "/v1/user/{userName}", GetUserByUserNameOrEmailAsync)
                .Description("Get a user object by username or email.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(User.Example());

            CreateRoute("GetLicenseTypeForUser", HttpMethod.Get, "/v1/users/{userId}/license-types", GetLicenseTypeForUserAsync)
                .Description("Retrieves license type for User")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(License.Manager.Models.LicenseType.Default);

            CreateRoute("GetGuestUsersForTenant", HttpMethod.Get, "/v1/users/guests", GetGuestUsersForTenantAsync)
                .Description("Gets a guest User Resource for the specified Tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new PagingMetadata<UserResponse> { List = new List<UserResponse> { UserResponse.Example() } });

            CreateRoute("UpdateUser", HttpMethod.Put, "/v1/users/{id:guid}", UpdateUserAsync)
                .Description("Update a User resource")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(UpdateUserRequest.Example())
                .ResponseFormat(UserResponse.Example());

            CreateRoute("ResendUserWelcomeEmail", HttpMethod.Post, "/v1/users/resendwelcomemail", ResendUserWelcomeEmailAsync)
                .Description("Resend Welcome Email to the User")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(ResendEmailRequest.Example())
                .ResponseFormat(new bool());

            CreateRoute("SendResetPasswordEmail", HttpMethod.Post, "/v1/users/sendresetpasswordemail", SendResetPasswordEmail)
                .Description("Send reset paasword Email to the User")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(PasswordResetEmailRequest.Example())
                .ResponseFormat(new bool());

            CreateRoute("DeleteUser", HttpMethod.Delete, "/v1/users/{id:guid}", DeleteUserAsync)
                .Description("Deletes a User resource.")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("PromoteGuest", HttpMethod.Post, "/v1/users/{userIdzzz}/promote", PromoteGuestAsync)
                .Description("Promotes a Guest User")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(PromoteGuestRequest.Example());

            CreateRoute("AutoProvisionRefreshGroups", HttpMethod.Post, "/v1/users/autoprovisionrefreshgroups", AutoProvisionRefreshGroupsAsync)
                .Description("Autoprovisions the refresh groups")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(IdpUserRequest.Example())
                .ResponseFormat(UserResponse.Example());
        }

        private async Task<object> LockUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid id = input.userId;
            User newUser;
            try
            {
                newUser = this.Bind<User>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to lock/unlock a User resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result =await _userController.LockOrUnlockUserAsync(id, newUser.IsLocked);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Error occured", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to Lock/Unlock user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerLockUser);
            }
        }

        private async Task<object> CreateUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            CreateUserRequest createUserRequest;
            try
            {
                createUserRequest = this.Bind<CreateUserRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                UserResponse userResponse;
                if (string.IsNullOrWhiteSpace(createUserRequest.ProjectAccessCode))
                {
                    userResponse = await _userController.CreateUserAsync(createUserRequest, TenantId, PrincipalId);
                }
                else
                {
                    userResponse = await _userController.CreateGuestAsync(createUserRequest, TenantId, PrincipalId);
                }

                return Negotiate
                    .WithModel(userResponse)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (UserExistsException ex)
            {
                Logger.Error("Failed to create user because a user already exists.", ex);
                return Response.UserExists(ex.Message);
            }
            catch (UserNotInvitedException ex)
            {
                Logger.Error("Failed to create user because the user has not been invited yet.", ex);
                return Response.UserNotInvited(ex.Message);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetUserByIdAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId = input.Id;
            try
            {
                return await _userController.GetUserAsync(userId);
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
                Logger.Error("GetUserById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersBasicAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                var getUsersParams = this.Bind<GetUsersParams>();
                return await _userController.GetUsersBasicAsync(TenantId, PrincipalId, getUsersParams);

            }
            catch (Exception ex)
            {
                Logger.Error("GetUsersBasic threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserByIdBasicAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId = input.Id;
            try
            {
                return await _userController.GetUserAsync(userId);
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
                Logger.Error("GetUserByIdBasic threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }


        private async Task<object> GetUserByUserNameOrEmailAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            string userName = input.userName;
            try
            {
                return await _userController.GetUserByUserNameOrEmailAsync(userName);
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
                Logger.Error("GetUserByUserNameOrEmailAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersForAccountAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            GetUsersParams getUsersParams;
            try
            {
                getUsersParams = this.Bind<GetUsersParams>();
            }

            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _userController.GetUsersForAccountAsync(getUsersParams, TenantId, PrincipalId);
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
                Logger.Error("Failed to get users due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersByIdsAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            IEnumerable<Guid> userIds;
            try
            {
                userIds = this.Bind<IEnumerable<Guid>>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to fetch users", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _userController.GetUsersByIdsAsync(userIds);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Validation failed while attempting to GetUsersByIds.", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get users due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUsers);
            }
        }

        private async Task<object> ResendUserWelcomeEmailAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            ResendEmailRequest basicUser;
            try
            {
                basicUser = this.Bind<ResendEmailRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a User resource.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userController.ResendUserWelcomeEmailAsync(basicUser.Email, basicUser.FirstName);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Error occured", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send email due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorResendWelcomeMail);
            }
        }

        private async Task<object> SendResetPasswordEmail(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            PasswordResetEmailRequest emailRequest;
            try
            {
                emailRequest = this.Bind<PasswordResetEmailRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a User resource.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userController.SendResetPasswordEmail(emailRequest);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Error occured", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send email due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorResendWelcomeMail);
            }
        }

        private async Task<object> UpdateUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId;
            UpdateUserRequest userModel;
            try
            {
                userId = Guid.Parse(input.id);
                userModel = this.Bind<UpdateUserRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a User resource.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
               return await _userController.UpdateUserAsync(userId, userModel);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a User resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateUser);
            }
        }

        private async Task<object> DeleteUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId = input.id;

            try
            {
                await _userController.DeleteUserAsync(userId);

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
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to delete a User resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorDeleteUser);
            }
        }

        private async Task<object> CanPromoteUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            string email = input.email;
            try
            {
                var result = await _userController.CanPromoteUserAsync(email);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException ex)
            {
                Logger.Error("User not found", ex);
                return Response.NotFound();
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while determining a User can be promoted", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorDeleteUser);
            }
        }

        private async Task<object> PromoteGuestAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            PromoteGuestRequest promoteRequest;
            try
            {
                promoteRequest = this.Bind<PromoteGuestRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to promote guest", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userController.PromoteGuestUserAsync(promoteRequest.UserId, TenantId, promoteRequest.LicenseType);

                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (PromotionFailedException ex)
            {
                return Response.Forbidden(ResponseReasons.PromotionFailed, "FAILED", ex.Message);
            }
            catch (LicenseAssignmentFailedException ex)
            {
                return Response.Forbidden(ResponseReasons.LicenseAssignmentFailed, "FAILED", ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to promote a user due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetGuestUsersForTenantAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            GetUsersParams getGuestUsersParams;
            try
            {
                getGuestUsersParams = this.Bind<GetUsersParams>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to get geust users", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _userController.GetGuestUsersForTenantAsync(TenantId, getGuestUsersParams);
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
                Logger.Error("Failed to get guest users due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestUser);
            }
        }

        private async Task<object> AutoProvisionRefreshGroupsAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            IdpUserRequest idpUserRequest;
            try
            {
                idpUserRequest = this.Bind<IdpUserRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to auto provision and refresh groups.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userController.AutoProvisionRefreshGroupsAsync(idpUserRequest, TenantId, PrincipalId);

                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (IdpUserProvisioningException ex)
            {
                Logger.Error("Failed to auto provision and refresh groups", ex);
                return Response.Forbidden(ResponseReasons.IdpUserAutoProvisionError);
            }
            catch (PromotionFailedException ex)
            {
                Logger.Error("Failed to auto provision and refresh groups", ex);
                return Response.Forbidden(ResponseReasons.PromotionFailed);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to auto provision and refresh groups", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> CreateUserGroupAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            CreateUserGroupRequest newUserGroupRequest;

            try
            {
                newUserGroupRequest = this.Bind<CreateUserGroupRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a User Group resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                var result = await _userController.CreateUserGroupAsync(newUserGroupRequest, TenantId, PrincipalId);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "CreateUserGroup: No valid Tenant level or User level access to groups!");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create User Group resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetGroupUsersAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid groupId = input.id;

            try
            {
                var result = await _userController.GetGroupUsersAsync(groupId, TenantId, PrincipalId);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.UserGroupNotFound);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("GetUserGroupsForGroup threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserGroupsForUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId = input.userId;
            try
            {
                var result = await _userController.GetGroupsForUserAsync(userId);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.UserGroupNotFound);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("GetUserGroupsForUser threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> RemoveUserFromPermissionGroupAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId = input.userId;
            Guid groupId = input.groupId;

            try
            {
                var result = await _userController.RemoveUserFromPermissionGroupAsync(userId, groupId, PrincipalId);
                if (!result)
                {
                    return Response.BadRequest("Either you don't have permission or cannot delete the last non locked super admin of this group.");
                }

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
            catch (DocumentNotFoundException ex)
            {
                Logger.Error("User could not be found", ex);
                return Response.NotFound();
            }
            catch (Exception ex)
            {
                Logger.Error("RemoveUserFromGroup threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetTenantIdByUserEmailAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            string email = input.email;

            try
            {
                var result = await _userController.GetTenantIdByUserEmailAsync(email);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.TenantNotFound);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("GetTenantIdByUserEmail threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetLicenseTypeForUserAsync(dynamic input)
        {
            await RequiresAccess()
                .WithPrincipalIdExpansion(_ => PrincipalId)
                .ExecuteAsync(CancellationToken.None);

            Guid userId = input.userId;

            try
            {
                var result = await _userController.GetLicenseTypeForUserAsync(userId, TenantId);
                return result;
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.TenantNotFound);
            }
            catch (InvalidOperationException)
            {
                return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetLicenseTypeForUser: Not authorized to call this route!");
            }
            catch (Exception ex)
            {
                Logger.Error("GetLicenseTypeForUser threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }
    }
}
