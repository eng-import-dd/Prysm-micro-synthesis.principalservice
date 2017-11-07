using FluentValidation;
using Nancy;
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
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.PrincipalService.Requests;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Workflow.Exceptions;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UsersModule : AbstractModule
    {
        private const string TenantIdClaim = "TenantId";
        private const string UserIdClaim = "UserId";
        private const string IsGuestClaim = "IsGuest";
        private const string GuestProjectIdClaim = "GuestProjectId";
        private readonly IUsersController _userController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;
        private const string LegacyBaseRoute = "/api";
        private const string DeprecationWarning = "DEPRECATED";

        public UsersModule(
            IMetadataRegistry metadataRegistry,
            IUsersController userController,
            ILoggerFactory loggerFactory)
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _userController = userController;
            _logger = loggerFactory.GetLogger(this);

            this.RequiresAuthentication();

            // Initialize documentation
            SetupRouteMetadata();
            SetupRoute_GetUsersForAccount();
            SetupRoute_UpdateUser();
            SetupRouteMetadata_LockUser();
            SetupRoute_CreateUserGroup();
            SetupRoute_CanPromoteUser();
            SetupRoute_GetGroupUsers();
            SetupRoute_GetGroupsForUser();
            SetupRoute_GetTenantIdByUserEmail();
            SetupRoute_RemoveUserFromPermissionGroup();
            // CRUD routes
            Post("/v1/users", CreateUserAsync, null, "CreateUser");
            Post("/api/v1/users", CreateUserAsync, null, "CreateUserLegacy");

            SetupRoute_GetUserById();
            SetupRoute_GetUsersByIds();
            SetupRoute_GetUsersBasic();
            SetupRoute_GetUserByIdBasic();
            SetupRoute_GetLicenseTypeForUser();

            Get("/v1/users/guests", GetGuestUsersForTenant, null, "GetGuestUsersForTenant");
            Get("api/v1/users/guests", GetGuestUsersForTenant, null, "GetGuestUsersForTenantLegacy");

            Put("/v1/users/{id:guid}", UpdateUserAsync, null, "UpdateUser");
            Put("/api/v1/users/{id:guid}", UpdateUserAsync, null, "UpdateUserLegacy");

            SetupRoute_ResendUserWelcomeEmailAsync();
            Delete("/v1/users/{id:guid}", DeleteUserAsync, null, "DeleteUser");
            Delete("/api/v1/users/{id:guid}", DeleteUserAsync, null, "DeleteUserLegacy");

            Post("/v1/users/{userId}/promote", PromoteGuestAsync, null, "PromoteGuest");
            Post("/api/v1/users/{userId}/promote", PromoteGuestAsync, null, "PromoteGuestLegacy");

            Post("/v1/users/autoprovisionrefreshgroups", AutoProvisionRefreshGroupsAsync, null, "AutoProvisionRefreshGroupsAsync");
            Post("/api/v1/users/autoprovisionrefreshgroups", AutoProvisionRefreshGroupsAsync, null, "AutoProvisionRefreshGroupsAsyncLegacy");

            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        #region Route Setup

        private void SetupRoute_GetUsersForAccount()
        {
            const string path = "/v1/users/";
            Get(path, GetUsersForAccount, null, "GetUsersForAccount");
            Get(LegacyBaseRoute + path, GetUsersForAccount, null, "GetUsersForAccountLegacy");
            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new GetUsersParams());
            var metadataResponse = ToFormattedJson(new PagingMetadata<UserResponse> {  List = new List<UserResponse>()});
            var metadataDescription = "Retrieve all Users resource.";
            _metadataRegistry.SetRouteMetadata("GetUsers", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });
            _metadataRegistry.SetRouteMetadata("GetUsersLegacy", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_ResendUserWelcomeEmailAsync()
        {
            const string path = "/v1/users/resendwelcomemail";
            Post(path, ResendUserWelcomeEmailAsync, null, "ResendUserWelcomeEmail");
            Post(LegacyBaseRoute + path, ResendUserWelcomeEmailAsync, null, "ResendUserWelcomeEmailLegacy");
            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new ResendEmailRequest());
            var metadataResponse = ToFormattedJson(new bool());
            var metadataDescription = "Resend Welcome Email to the User.";
            _metadataRegistry.SetRouteMetadata("ResendUserWelcomeEmail", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });
            _metadataRegistry.SetRouteMetadata("ResendUserWelcomeEmailLegacy", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_UpdateUser()
        {
            const string path = "/v1/users/{id:guid}";
            Put(path, UpdateUserAsync, null, "UpdateUser");
            Put(LegacyBaseRoute + path, UpdateUserAsync, null, "UpdateUserLegacy");
            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new UpdateUserRequest());
            var metadataResponse = ToFormattedJson(new UserResponse());
            var metadataDescription = "Update User resource.";
            _metadataRegistry.SetRouteMetadata("UpdateUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });
            _metadataRegistry.SetRouteMetadata("UpdateUserLegacy", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_CanPromoteUser()
        {
            const string path = "/v1/users/canpromoteuser/{0}";
            Get(path, CanPromoteUser, null, "CanPromoteUser");
            Get(LegacyBaseRoute + path, CanPromoteUser, null, "CanPromoteUserLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(string.Empty);
            var metadataResponse = ToFormattedJson(new CanPromoteUserResponse());
            var metadataDescription = "States whether a user can be promoted";

            _metadataRegistry.SetRouteMetadata("CanPromoteUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("CanPromoteUserLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }
        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("CreateUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Create a new User",
                Description = ""
            });

            _metadataRegistry.SetRouteMetadata("UpdateUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Update User",
                Description = "Update a specific User resource."
            });

            _metadataRegistry.SetRouteMetadata("DeleteUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Delete User",
                Description = "Delete a specific User resource."
            });

            _metadataRegistry.SetRouteMetadata("PromoteGuest", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Promote Guest",
                Description = "Promote a guest to licensed User."
            });

            _metadataRegistry.SetRouteMetadata("GetGuestUsersForTenant", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new []{HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError},
                Response = "Get Guest User",
                Description = "Retrive all the guest users resource for a tenant."
            });

            _metadataRegistry.SetRouteMetadata("AutoProvisionRefreshGroups", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "AutoProvision and Refresh Groups",
                Description = "AutoProvisions and Refreshes Groups."
            });
        }

        /// <summary>
        /// Setups the route for Get User by Id.
        /// </summary>
        private void SetupRoute_GetUserById()
        {
            const string path = "/v1/users/{id:guid}";
            Get(path, GetUserById, null, "GetUserById");
            Get("/api/" + path, GetUserById, null, "GetUserByIdLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new UserResponse());
            var metadataDescription = "Retrieves a user by User Id";

            _metadataRegistry.SetRouteMetadata("GetUserById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUserByIdLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        /// <summary>
        /// Setups the route for Get User Basic data.
        /// </summary>
        private void SetupRoute_GetUsersBasic()
        {
            const string path = "/v1/users/basic";
            Get(path, GetUsersBasic, null, "GetUsersBasic");
            Get("/api/" + path, GetUsersBasic, null, "GetUsersBasicLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new GetUsersParams());
            var metadataResponse = ToFormattedJson(new PagingMetadata<BasicUserResponse> { List = new List<BasicUserResponse>() });
            var metadataDescription = "Retrieves a users basic details";

            _metadataRegistry.SetRouteMetadata("GetUsersBasic", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUsersBasicLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetUserByIdBasic()
        {
            const string path = "/v1/users/{userId:guid}/basic";
            Get(path, GetUserByIdBasic, null, "GetUserByIdBasic");
            Get("/api/" + path, GetUserByIdBasic, null, "GetUserByIdBasicLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new UserResponse());
            var metadataDescription = "Retrieves a user basic details by id";

            _metadataRegistry.SetRouteMetadata("GetUserByIdBasic", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUserByIdBasicLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRouteMetadata_LockUser()
        {
            const string path = "/v1/users/{userId:guid}/lock";
            Post(path, LockUserAsync, null, "LockuserAsync");
            Post(LegacyBaseRoute + path, LockUserAsync, null, "LockuserAsyncLegacy");
            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new User());
            var metadataResponse = ToFormattedJson(new bool());
            var metadataDescription = "Locks the respective user";
            _metadataRegistry.SetRouteMetadata("LockUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });
            _metadataRegistry.SetRouteMetadata("LockUserLegacy", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_CreateUserGroup()
        {
            const string path = "/v1/usergroups";
            Post(path, CreateUserGroup, null, "CreateUserGroup");
            Post("/api/" + path, CreateUserGroup, null, "CreateUserGroupLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new CreateUserGroupRequest());
            var metadataResponse = ToFormattedJson(new User());
            var metadataDescription = "Creates User Group";

            _metadataRegistry.SetRouteMetadata("CreateUserGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("CreateUserGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetGroupUsers()
        {
            const string path = "/v1/groups/{id}/users";
            Get(path, GetGroupUsers, null, "GetGroupUsers");
            Get("/api" + path, GetGroupUsers, null, "GetGroupUsersLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Found, HttpStatusCode.NotFound };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new List<Guid> {new Guid()});
            var metadataDescription = "Retrieves user groups by group Id";

            _metadataRegistry.SetRouteMetadata("GetUserGroupsForGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUserGroupsForGroupLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetGroupsForUser()
        {
            const string path = "/v1/users/{userId}/groups";
            Get(path, GetUserGroupsForUserAsync, null, "GetGroupsForUser");
            Get(LegacyBaseRoute + path, GetUserGroupsForUserAsync, null, "GetGroupsForUserLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(new List<Guid> { new Guid() });
            var metadataDescription = "Retrieves user groups by user Id";

            _metadataRegistry.SetRouteMetadata("GetGroupsForUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetGroupsForUserLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetTenantIdByUserEmail()
        {
            const string path = "/v1/users/tenantid/{email}";
            Get(path, GetTenantIdByUserEmail, null, "GetTenantIdByUserEmail");
            Get("/api" + path, GetTenantIdByUserEmail, null, "GetTenantIdByUserEmailLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound };
            var metadataRequest = ToFormattedJson(string.Empty);
            var metadataResponse = ToFormattedJson(new Guid());
            var metadataDescription = "Retrieves tenant id by user email";

            _metadataRegistry.SetRouteMetadata("GetTenantIdByUserEmail", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetTenantIdByUserEmail", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetLicenseTypeForUser()
        {
            const string path = "/v1/users/{userId}/license-types";
            Get(path, GetLicenseTypeForUser, null, "GetLicenseTypeForUser");
            Get("/api" + path, GetLicenseTypeForUser, null, "GetLicenseTypeForUserLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound };
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataResponse = ToFormattedJson(License.Manager.Models.LicenseType.Default);
            var metadataDescription = "Retrieves license type for User";

            _metadataRegistry.SetRouteMetadata("GetLicenseTypeForUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetLicenseTypeForUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_RemoveUserFromPermissionGroup()
        {
            const string path = "v1/groups/{groupId}/users/{userId}";
            Delete(path, RemoveUserFromPermissionGroupAsync, null, "RemoveUserFromPermissionGroup");
            Delete(LegacyBaseRoute + path, RemoveUserFromPermissionGroupAsync, null, "RemoveUserFromPermissionGroupLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = ToFormattedJson(new bool());
            var metadataRequest = ToFormattedJson(new Guid());
            var metadataDescription = "Removes a specific user from the group";

            _metadataRegistry.SetRouteMetadata("RemoveUserFromPermissionGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("RemoveUserFromPermissionGroupLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetUsersByIds()
        {
            Post(Routing.GetUsersByIds, GetUsersByIds, null, RouteNames.GetUsersByIds);
            Post($"{LegacyBaseRoute}{Routing.GetUsersByIds}", GetUsersByIds, null, RouteNames.GetUsersByIdsLegacy);

            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(Enumerable.Empty<Guid>());
            var metadataResponse = ToFormattedJson(new List<User> { new User() });
            var metadataDescription = "Returns matching users from a list of user ids";

            _metadataRegistry.SetRouteMetadata(RouteNames.GetUsersByIds, new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });
        }
        #endregion

        private async Task<object> LockUserAsync(dynamic input)
        {
            Guid id = input.userId;
            User newUser;
            try
            {
                newUser = this.Bind<User>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to lock/unlock a User resource", ex);
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
                _logger.Error("Error occured", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundUser);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to Lock/Unlock user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerLockUser);
            }
        }

        private async Task<object> CreateUserAsync(dynamic input)
        {
            CreateUserRequest newUser;
            try
            {
                newUser = this.Bind<CreateUserRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var createdBy);

                var result = await _userController.CreateUserAsync(newUser, tenantId, createdBy);
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
                _logger.Error("Failed to create user resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetUserById(dynamic input)
        {
            Guid userId = input.Id;
            try
            {
                Boolean.TryParse(Context.CurrentUser.FindFirst(IsGuestClaim).Value, out var isGuest);
                if (isGuest)
                {
                    return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetUserById: Guest user is not authorized to call this route!");
                }

                //TODO: Call Projects Microservice to get project level access result here. Currently hard coding to 1 (Success) - Yusuf
                var resultCode = await ValidUserLevelAccess(userId);
                if (resultCode != HttpStatusCode.OK)
                {
                    return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetUserById: No valid user level access to project!");
                }

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
                _logger.Error("GetUserById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersBasic(dynamic input)
        {
            try
            {
                var getUsersParams = this.Bind<GetUsersParams>();
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);
                return await _userController.GetUsersBasicAsync(tenantId, userId, getUsersParams);

            }
            catch (Exception ex)
            {
                _logger.Error("GetUsersBasic threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserByIdBasic(dynamic input)
        {
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
                _logger.Error("GetUserByIdBasic threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersForAccount(dynamic input)
        {
            GetUsersParams getUsersParams;
            try
            {
                getUsersParams = this.Bind<GetUsersParams>();
            }

            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var currentUserId);
                return await _userController.GetUsersForAccountAsync(getUsersParams, tenantId, currentUserId);
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
                _logger.Error($"Failed to get users due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersByIds(dynamic input)
        {
            IEnumerable<Guid> userIds;
            try
            {
                userIds = this.Bind<IEnumerable<Guid>>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to fetch users", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _userController.GetUsersByIdsAsync(userIds);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get users due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUsers);
            }
        }

        private async Task<object> ResendUserWelcomeEmailAsync(dynamic input)
        {
            ResendEmailRequest basicUser;
            try
            {
                basicUser = this.Bind<ResendEmailRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to update a User resource.", ex);
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
                _logger.Error("Error occured", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to send email due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorResendWelcomeMail);
            }

        }

        private async Task<object> UpdateUserAsync(dynamic input)
        {
            Guid userId;
            UpdateUserRequest userModel;
            try
            {
                userId = Guid.Parse(input.id);
                userModel = this.Bind<UpdateUserRequest>();
                var resultCode = await ValidUserLevelAccess(userId, requiredPermission: PermissionEnum.CanEditUser);
                if (resultCode != HttpStatusCode.OK)
                {
                    return Response.Unauthorized("Unauthorized", resultCode.ToString(), "UpdateUser: Error occured!");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to update a User resource.", ex);
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
                _logger.Error("Unhandled exception encountered while attempting to update a User resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateUser);
            }
        }

        private async Task<object> DeleteUserAsync(dynamic input)
        {
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
                _logger.Error("Unhandled exception encountered while attempting to delete a User resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorDeleteUser);
            }
        }

        private async Task<object> CanPromoteUser(dynamic input)
        {
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
                _logger.Error("User not found", ex);
                return Response.NotFound();
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while determining a User can be promoted", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorDeleteUser);
            }
        }

        private async Task<HttpStatusCode> ValidUserLevelAccess(Guid accessedUserId, PermissionEnum requiredPermission = PermissionEnum.CanViewUsers)
        {
            Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var currentUserId);
            Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
            if (currentUserId == accessedUserId)
            {
                return HttpStatusCode.OK;
            }
            try
            {
             var accessedUser = await _userController.GetUserAsync(accessedUserId);
            if (accessedUserId == Guid.Empty || accessedUser == null)
            {
                return HttpStatusCode.NotFound;
            }
            //TODO: address the code once permission service dependency is implemented
            //var userPermissions = new Lazy<List<PermissionEnum>>(InitUserPermissionsList);

                if (tenantId == accessedUser.TenantId)
                {
                    return HttpStatusCode.OK;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"An error occurred validating access for user: {accessedUserId}", ex);
                throw;
            }

            return HttpStatusCode.Unauthorized;
        }

        private async Task<object> PromoteGuestAsync(dynamic input)
        {
            PromoteGuestRequest promoteRequest;
            try
            {
                promoteRequest = this.Bind<PromoteGuestRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to promote guest", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);

                var result = await _userController.PromoteGuestUserAsync(promoteRequest.UserId, tenantId, promoteRequest.LicenseType);

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
                _logger.Error("Failed to promote a user due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetGuestUsersForTenant(dynamic input)
        {
            GetUsersParams getGuestUsersParams;
            try
            {
                getGuestUsersParams = this.Bind<GetUsersParams>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to get geust users", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                return await _userController.GetGuestUsersForTenantAsync(tenantId, getGuestUsersParams);
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
                _logger.Error($"Failed to get guest users due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestUser);
            }
        }

        private async Task<object> AutoProvisionRefreshGroupsAsync(dynamic input)
        {
            IdpUserRequest idpUserRequest;
            try
            {
                idpUserRequest = this.Bind<IdpUserRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to auto provision and refresh groups.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var createdBy);

                var result = await _userController.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy);

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
                _logger.Error("Failed to auto provision and refresh groups", ex);
                return Response.Forbidden(ResponseReasons.IdpUserAutoProvisionError);
            }
            catch (PromotionFailedException ex)
            {
                _logger.Error("Failed to auto provision and refresh groups", ex);
                return Response.Forbidden(ResponseReasons.PromotionFailed);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to auto provision and refresh groups", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        #region User Group Methods

        private async Task<object> CreateUserGroup(dynamic input)
        {
            CreateUserGroupRequest newUserGroupRequest;

            try
            {
                newUserGroupRequest = this.Bind<CreateUserGroupRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to create a User Group resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);

                var result = await _userController.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId);
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
                _logger.Error("Failed to create User Group resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }

        private async Task<object> GetGroupUsers(dynamic input)
        {
            Guid groupId = input.id;

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);

                var result = await _userController.GetGroupUsersAsync(groupId, tenantId, userId);
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
                _logger.Error("GetUserGroupsForGroup threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUserGroupsForUserAsync(dynamic input)
        {
            Guid userId = input.userId;
            var resultCode = await ValidUserLevelAccess(userId, requiredPermission: PermissionEnum.CanAddRemoveUsersFromGroup);
            if (resultCode != HttpStatusCode.OK)
            {
                return resultCode;
            }

            try
            {
                var result = await _userController.GetGroupsForUserAsync(userId);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.Found);
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
                _logger.Error("GetUserGroupsForUser threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> RemoveUserFromPermissionGroupAsync(dynamic input)
        {
                Guid userId = input.userId;
                Guid groupId = input.groupId;
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var currentUserId);
                var result = await _userController.RemoveUserFromPermissionGroupAsync(userId, groupId, currentUserId);
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
                _logger.Error("User could not be found", ex);
                return Response.NotFound();
            }
            catch (Exception ex)
            {
                _logger.Error("RemoveUserFromGroup threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetTenantIdByUserEmail(dynamic input)
        {
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
                _logger.Error("GetTenantIdByUserEmail threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        #endregion

        #region User License Type Method

        private async Task<object> GetLicenseTypeForUser(dynamic input)
        {
            Guid userId = input.userId;

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);

                var result = await _userController.GetLicenseTypeForUserAsync(userId, tenantId);
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
                _logger.Error("GetLicenseTypeForUser threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        #endregion
    }
}
