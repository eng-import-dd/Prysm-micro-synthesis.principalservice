using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Nancy;
using Nancy.ErrorHandling;
using Nancy.ModelBinding;
using Synthesis.DocumentStorage;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Controllers.Exceptions;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.Extensions;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.TenantService.InternalApi.Api;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UsersModule : SynthesisModule
    {
        private readonly IUsersController _userController;
        private readonly IGroupsController _groupsController;
        private readonly ITenantApi _tenantApi;

        public UsersModule(
            IUsersController userController,
            IGroupsController groupsController,
            ITenantApi tenantApi,
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(ServiceInformation.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _userController = userController;
            _groupsController = groupsController;
            _tenantApi = tenantApi;

            CreateRoute("CreateUser", HttpMethod.Post, Routing.Users, CreateUserAsync)
                .Description("Create a new EnterpriseUser or TrialUser resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(CreateUserRequest.Example())
                .ResponseFormat(User.Example());

            CreateRoute("CreateGuest", HttpMethod.Post, Routing.Guests, CreateGuestUserAsync)
                .Description("Create a new GuestUser resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(CreateUserRequest.GuestUserExample())
                .ResponseFormat(CreateGuestUserResponse.Example());

            CreateRoute("SendGuestVerificationEmailAsync", HttpMethod.Post, Routing.SendVerificationEmail, SendGuestVerificationEmailAsync)
                .Description("Send a verification email to a guest")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(GuestVerificationEmailRequest.Example());

            CreateRoute("GetUsersForTenant", HttpMethod.Post, Routing.GetUsers, GetUsersForTenantAsync)
                .Description("Retrieve all Users resource")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(UserFilteringOptions.Example())
                .ResponseFormat(new PagingMetadata<User> { List = new List<User> { User.Example() } });

            CreateRoute("UpdateUser", HttpMethod.Put, Routing.UsersWithId, UpdateUserAsync)
                .Description("Update a User resource")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(User.Example())
                .ResponseFormat(User.Example());

            CreateRoute("GetUserNames", HttpMethod.Post, Routing.GetUserNames, GetUserNamesAsync)
                .Description("Gets the first and last name for each of the supplied userids")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new List<Guid> { Guid.Empty })
                .ResponseFormat(new List<UserNames> { UserNames.Example() });

            CreateRoute("LockUser", HttpMethod.Post, Routing.LockUser, LockUserAsync)
                .Description("Locks the respective user")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(User.Example())
                .ResponseFormat(new bool());

            CreateRoute("CreateUserGroup", HttpMethod.Post, Routing.UserGroups, CreateUserGroupAsync)
                .Description("Creates a User Group")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(UserGroup.Example())
                .ResponseFormat(UserGroup.Example());

            CreateRoute("CanPromoteUser", HttpMethod.Get, Routing.PromoteUser, CanPromoteUserAsync)
                .Description("States whether a user can be promoted")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(CanPromoteUser.Example());

            CreateRoute("GetUserIdsByGroupId", HttpMethod.Get, Routing.UserIdsByGroupId, GetUserIdsByGroupIdAsync)
                .Description("Retrieves user groups by group Id")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<Guid> { new Guid() });

            CreateRoute("GetGroupIdsByUserId", HttpMethod.Get, Routing.GroupIdsByUserId, GetGroupIdsByUserIdAsync)
                .Description("Retrieves user groups by user Id")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<Guid> { new Guid() });

            CreateRoute("RemoveUserFromPermissionGroup", HttpMethod.Delete, Routing.RemoveUserFromPermissionGroup, RemoveUserFromPermissionGroupAsync)
                .Description("Removes a specific user from the group")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(new bool());

            CreateRoute("GetUserById", HttpMethod.Get, Routing.UsersWithId, GetUserByIdAsync)
                .Description("Gets a User resource by UserId")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(User.Example());

            CreateRoute("GetUsersByIds", HttpMethod.Post, Routing.GetUsersByIds, GetUsersByIdsAsync)
                .Description("Get a Principal resource by it's identifier.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(new List<User> { User.Example() });

            CreateRoute("GetUsersBasic", HttpMethod.Post, Routing.UsersBasic, GetUsersBasicAsync)
                .Description("Retrieves a users basic details")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(UserFilteringOptions.Example())
                .ResponseFormat(new PagingMetadata<BasicUser> { List = new List<BasicUser> { BasicUser.Example() } });

            CreateRoute("GetUserCount", HttpMethod.Post, Routing.UserCount, GetUserCountAsync)
                .Description("Retrieves a users basic details")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(UserFilteringOptions.Example());

            CreateRoute("GetUserByIdBasic", HttpMethod.Get, Routing.UserByIdBasic, GetUserByIdBasicAsync)
                .Description("Get a Principal resource by it's identifier.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(User.Example());

            CreateRoute("GetUserByUserNameOrEmail", HttpMethod.Get, Routing.UserWithUsername, GetUserByUserNameOrEmailAsync)
                .Description("Get a user object by username or email.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(User.Example());

            CreateRoute("GetLicenseTypeForUser", HttpMethod.Get, Routing.LicenseTypeForUser, GetLicenseTypeForUserAsync)
                .Description("Retrieves license type for User")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .ResponseFormat(LicenseType.Default);

            CreateRoute("GetGuestUsersForTenant", HttpMethod.Post, Routing.GuestUsersForTenant, GetGuestUsersForTenantAsync)
                .Description("Gets a guest User Resource for the specified Tenant")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound)
                .RequestFormat(UserFilteringOptions.Example())
                .ResponseFormat(new PagingMetadata<User> { List = new List<User> { User.Example() } });

            CreateRoute("DeleteUser", HttpMethod.Delete, Routing.UsersWithId, DeleteUserAsync)
                .Description("Deletes a User resource.")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("PromoteGuest", HttpMethod.Post, Routing.PromoteGuest, PromoteGuestAsync)
                .Description("Promotes a Guest User")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(LicenseType.UserLicense);

            CreateRoute("AutoProvisionRefreshGroups", HttpMethod.Post, Routing.AutoProvisionRefreshGroups, AutoProvisionRefreshGroupsAsync)
                .Description("Autoprovisions the refresh groups")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(IdpUserRequest.Example())
                .ResponseFormat(User.Example());

            CreateRoute("VerifyEmail", HttpMethod.Post, Routing.VerifyEmail, VerifyEmailAsync)
                .Description("Verifies the email of a newly created user")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(VerifyUserEmailRequest.Example())
                .ResponseFormat(VerifyUserEmailResponse.Example());
        }

        private async Task<object> VerifyEmailAsync(dynamic input, CancellationToken cancellationToken)
        {
            VerifyUserEmailRequest verifyRequest;

            try
            {
                verifyRequest = this.Bind<VerifyUserEmailRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to verify an email", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _userController.VerifyEmailAsync(verifyRequest);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (Exception ex)
            {
                Logger.Error("An error occured while attempting to verify the email.", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorVerifyEmail);
            }
        }

        private async Task<object> LockUserAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.userId;
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

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _userController.LockOrUnlockUserAsync(userId, TenantId, newUser.IsLocked);
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

        private async Task<object> CreateUserAsync(dynamic input, CancellationToken cancellationToken)
        {
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

            createUserRequest.ReplaceNullOrEmptyTenantId(TenantId);

            await RequiresAccess()
                .WithTenantIdExpansion(ctx => createUserRequest.TenantId.GetValueOrDefault())
                .ExecuteAsync(cancellationToken);

            try
            {
                var userResponse = await _userController.CreateUserAsync(createUserRequest, Context.CurrentUser);

                return Negotiate
                    .WithModel(userResponse)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (TenantMappingException ex)
            {
                Logger.Error("Failed to create user, adding user to tenant failed.", ex);
                return Response.TenantMappingFailed(ex.Message);
            }
            catch (IdentityPasswordException ex)
            {
                Logger.Error("Failed to create user, setting the user's password failed.", ex);
                return Response.SetPasswordFailed(ex.Message);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Info("Bad request while creating user", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> CreateGuestUserAsync(dynamic input, CancellationToken cancellationToken)
        {
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
                var userResponse = await _userController.CreateGuestUserAsync(createUserRequest);

                return Negotiate
                    .WithModel(userResponse)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (UserExistsException ex)
            {
                Logger.Error("Failed to create user because a user already exists.", ex);
                return Response.UserExists(ex.Message);
            }
            catch (IdentityPasswordException ex)
            {
                Logger.Error("Failed to create user, setting the user's password failed.", ex);
                return Response.SetPasswordFailed(ex.Message);
            }
            catch (SendEmailException ex)
            {
                Logger.Error("Failed to create user, sending a verification email failed.", ex);
                return Response.SendEmailFailed(ex.Message);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Validation failed while attempting to create a GuestUser resource.", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> SendGuestVerificationEmailAsync(dynamic input, CancellationToken cancellationToken)
        {
            GuestVerificationEmailRequest sendEmailRequest;
            try
            {
                sendEmailRequest = this.Bind<GuestVerificationEmailRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                await _userController.SendGuestVerificationEmailAsync(sendEmailRequest);

                return Negotiate
                    .WithStatusCode(HttpStatusCode.OK);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Info("Validation failed while attempting to create a GuestUser resource.", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (EmailAlreadyVerifiedException ex)
            {
                Logger.Info("Email not sent because it is already been verified.", ex);
                return Response.EmailAlreadyVerified(ex.Message);
            }
            catch (EmailRecentlySentException ex)
            {
                Logger.Info("Email not sent because it was sent too recently.", ex);
                return Response.EmailRecentlySent(ex.Message);
            }
            catch (NotFoundException ex)
            {
                Logger.Debug("Email was not sent because the user could not be found.", ex);
                return Response.NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetUserByIdAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.Id;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

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
                Logger.Info("Bad request getting user by id", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get user '{userId}' due to an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserNamesAsync(dynamic input, CancellationToken cancellationToken)
        {
            List<Guid> userIds;

            try
            {
                userIds = this.Bind<List<Guid>>();
            }
            catch (Exception ex)
            {
                Logger.Info("Binding failed while attempting to fetch user ids", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _userController.GetNamesForUsersAsync(userIds);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Info("Bad request getting user names", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get user name information for the following users due to an unhandled exception: {string.Join(", ", userIds)}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersBasicAsync(dynamic input, CancellationToken cancellationToken)
        {
            UserFilteringOptions userFilteringOptions;

            try
            {
                userFilteringOptions = this.Bind<UserFilteringOptions>();
            }
            catch (Exception ex)
            {
                Logger.Info("Binding failed while retrieving basic user information", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .WithTenantIdExpansion(ctx => TenantId)
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _userController.GetUsersBasicAsync(TenantId, PrincipalId, userFilteringOptions);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get filtered basic user information for tenant '{TenantId}' as principal '{PrincipalId}' due to an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserCountAsync(dynamic input, CancellationToken cancellationToken)
        {
            UserFilteringOptions userFilteringOptions;

            try
            {
                userFilteringOptions = this.Bind<UserFilteringOptions>();
            }
            catch (Exception ex)
            {
                Logger.Info("Binding failed while retrieving the user count", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                var count = await _userController.GetUserCountAsync(TenantId, PrincipalId, userFilteringOptions);
                return count.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to count users in tenant '{TenantId}' as principal '{PrincipalId}' due to an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserByIdBasicAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.Id;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _userController.GetUserAsync(userId);
            }
            catch (NotFoundException ex)
            {
                Logger.Info("User not found while getting basic user info", ex);
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Info("Bad request while getting basic user information", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get basic user information for '{userId}' due to an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserByUserNameOrEmailAsync(dynamic input, CancellationToken cancellationToken)
        {
            string userName = input.userName;

            // To work around nancy bug https://github.com/NancyFx/Nancy/issues/1280 https://github.com/NancyFx/Nancy/issues/1499
            // Usernames/emailIds will never have spaces in them, so this workaround shouldn't break anything
            userName = userName?.Replace(" ", "+");

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                var user = await _userController.GetUserByUserNameOrEmailAsync(userName);

                await RequiresAccess()
                    .WithPrincipalIdExpansion(ctx => user.Id.GetValueOrDefault())
                    .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(user.Id.GetValueOrDefault(), ct))
                    .ExecuteAsync(cancellationToken);

                return user;
            }
            catch (RouteExecutionEarlyExitException)
            {
                throw;
            }
            catch (NotFoundException ex)
            {
                Logger.Info("User not found while getting user by username or email", ex);
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get user information by username (or email) from tenant '{TenantId}' as principal '{PrincipalId}' due to an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersForTenantAsync(dynamic input, CancellationToken cancellationToken)
        {
            UserFilteringOptions userFilteringOptions;
            try
            {
                userFilteringOptions = this.Bind<UserFilteringOptions>();
            }
            catch (Exception ex)
            {
                Logger.Info("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _userController.GetUsersForTenantAsync(userFilteringOptions, TenantId, PrincipalId);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUsers);
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

        private async Task<object> GetUsersByIdsAsync(dynamic input, CancellationToken cancellationToken)
        {
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

            // TODO: Secure GetUsersByIdsAsync route better. A requester should be able to get information
            // only on certain users. As-is, this method allows any authenticated principal
            // to get data on all other users, assuming their Id's are known.

            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

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

        private async Task<object> UpdateUserAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.id;
            User userModel;
            try
            {
                userModel = this.Bind<User>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a User resource.", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

            try
            {
                return await _userController.UpdateUserAsync(userId, userModel, TenantId, Context.CurrentUser);
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

        private async Task<object> DeleteUserAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            Guid userId = input.id;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

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

        private async Task<object> CanPromoteUserAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            string email = input.email;
            try
            {
                var result = await _userController.CanPromoteUserAsync(email, TenantId);
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

        private async Task<object> PromoteGuestAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            LicenseType licenseType;
            try
            {
                licenseType = this.Bind<LicenseType>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to promote guest", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                await _userController.PromoteGuestUserAsync(input.userId, TenantId, licenseType, Context.CurrentUser);

                return Negotiate
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

        private async Task<object> GetGuestUsersForTenantAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            UserFilteringOptions userFilteringOptions;
            try
            {
                userFilteringOptions = this.Bind<UserFilteringOptions>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to get geust users", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _userController.GetGuestUsersForTenantAsync(TenantId, userFilteringOptions);
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

        private async Task<object> AutoProvisionRefreshGroupsAsync(dynamic input, CancellationToken cancellationToken)
        {
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

            await RequiresAccess()
                .WithTenantIdExpansion(ctx => idpUserRequest.TenantId)
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _userController.AutoProvisionRefreshGroupsAsync(idpUserRequest, idpUserRequest.TenantId, Context.CurrentUser);

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

        private async Task<object> CreateUserGroupAsync(dynamic input, CancellationToken cancellationToken)
        {
            await RequiresAccess()
                .ExecuteAsync(cancellationToken);

            UserGroup newUserGroupRequest;

            try
            {
                newUserGroupRequest = this.Bind<UserGroup>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a User Group resource", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(newUserGroupRequest.UserId, ct))
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _userController.CreateUserGroupAsync(newUserGroupRequest, PrincipalId);
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

        private async Task<object> GetUserIdsByGroupIdAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid groupId = input.id;

            await RequiresAccess()
                .WithTenantIdExpansion(async (ctx, ct) => await _groupsController.GetTenantIdForGroupIdAsync(groupId, TenantId, ct) ?? Guid.Empty)
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _userController.GetUserIdsByGroupIdAsync(groupId, PrincipalId);
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

        private async Task<object> GetGroupIdsByUserIdAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.userId;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

            try
            {
                var result = await _userController.GetGroupIdsByUserIdAsync(userId);
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

        private async Task<object> RemoveUserFromPermissionGroupAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.userId;
            Guid groupId = input.groupId;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

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

        private async Task<object> GetLicenseTypeForUserAsync(dynamic input, CancellationToken cancellationToken)
        {
            Guid userId = input.userId;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => userId)
                .WithAnyTenantIdExpansion(async (ctx, ct) => await GetTenantIdsForUserAsync(userId, ct))
                .ExecuteAsync(cancellationToken);

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

        private async Task<IEnumerable<Guid>> GetTenantIdsForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var response = await _tenantApi.GetTenantIdsForUserIdAsync(userId);
            if (!response.IsSuccess())
            {
                throw new Exception($"Failed to get tenant IDs for user '{userId}': ({response.ResponseCode}) {response.ErrorResponse?.Message ?? response.ReasonPhrase} ");
            }

            return response.Payload ?? Enumerable.Empty<Guid>();
        }
    }
}