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
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Enums;
using Synthesis.PrincipalService.Requests;
using Synthesis.Nancy.MicroService.Security;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UsersModule : NancyModule
    {
        private const string TenantIdClaim = "TenantId";
        private const string UserIdClaim = "UserId";
        private const string IsGuestClaim = "IsGuest";
        private const string GuestProjectIdClaim = "GuestProjectId";
        private readonly IUsersController _userController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;
        private const string LegacyBaseRoute = "/api";
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private const string DeprecationWarning = "DEPRECATED";

        public UsersModule(
            IMetadataRegistry metadataRegistry,
            IUsersController userController,
            ILogger logger)
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _userController = userController;
            _logger = logger;

            this.RequiresAuthentication();

            // Initialize documentation
            SetupRouteMetadata();
            SetupRoute_GetUsersForAccount();
            // CRUD routes
            Post("/v1/users", CreateUserAsync, null, "CreateUser");
            Post("/api/v1/users", CreateUserAsync, null, "CreateUserLegacy");

            SetupRoute_GetUserById();
            SetupRoute_GetUsersBasic();
            SetupRoute_GetUserByIdBasic();

            Put("/v1/users/{id:guid}", UpdateUserAsync, null, "UpdateUser");
            Put("/api/v1/users/{id:guid}", UpdateUserAsync, null, "UpdateUserLegacy");

            Delete("/v1/users/{id:guid}", DeleteUserAsync, null, "DeleteUser");
            Delete("/api/v1/users/{id:guid}", DeleteUserAsync, null, "DeleteUserLegacy");

            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }
        private void SetupRoute_GetUsersForAccount()
        {
            const string path = "/v1/users/";
            Get(path, GetUsersForAccount, with=>
                                          {
                                              var requestQuery = with.Request.Query;
                                              return true;
                                          } , "GetUsersForAccount");
            Get(LegacyBaseRoute + path, GetUsersForAccount, null, "GetUsersForAccountLegacy");
            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError };
            var metadataResponse = "Get Users";
            var metadataDescription = "Retrieve all Users resource.";
            _metadataRegistry.SetRouteMetadata("GetUsers", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });
            _metadataRegistry.SetRouteMetadata("GetUsersLegacy", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = metadataStatusCodes,
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
            var metadataResponse = _serializer.Serialize(new User());
            var metadataDescription = "Retrieves a user by User Id";

            _metadataRegistry.SetRouteMetadata("GetUserById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUserByIdLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
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
            var metadataResponse = _serializer.Serialize(new User());
            var metadataDescription = "Retrieves a users basic details";

            _metadataRegistry.SetRouteMetadata("GetUsersBasic", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUsersBasicLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
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
            var metadataResponse = _serializer.Serialize(new User());
            var metadataDescription = "Retrieves a user basic details by id";

            _metadataRegistry.SetRouteMetadata("GetUserByIdBasic", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetUserByIdBasicLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
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
                _logger.Warning("Binding failed while attempting to create a User resource", ex);
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
                    return Response.Unauthorized("Unauthorized", ResultCode.Unauthorized.ToString(), "GetUserById: Unauthorized method call!");
                }

                //TODO: Call Projects Microservice to get project level access result here. Currently hard coding to 1 (Success) - Yusuf
                var resultCode = ValidUserLevelAccess(userId);
                if (resultCode != ResultCode.Success)
                {
                    return Response.Unauthorized("Unauthorized", ResultCode.Unauthorized.ToString(), "GetUserById: No valid user level access to project!");
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
                _logger.LogMessage(LogLevel.Error, "GetUserById threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

       
        private async Task<object> GetUsersBasic(dynamic input)
        {
            try
            {
                GetUsersParams getUsersParams;
                getUsersParams = this.Bind<GetUsersParams>() ?? new GetUsersParams
                {
                    SearchValue = "",
                    PageNumber = 1,
                    PageSize = 10,
                    UserGroupingType = UserGroupingTypeEnum.None,
                    UserGroupingId = Guid.Empty,
                    ExcludeUsersInGroup = false,
                    OnlyCurrentUser = false,
                    IncludeInactive = false,
                    SortColumn = "FirstName",
                    SortOrder = DataSortOrder.Ascending,
                    IdpFilter = IdpFilterEnum.All
                };

                if (!getUsersParams.UserGroupingType.Equals(UserGroupingTypeEnum.None) && (getUsersParams.UserGroupingId.Equals(Guid.Empty)))
                {
                    return Response.Unauthorized("Unauthorized", "Missing Parameter Values", "GetUsersBasic: If the userGroupingType is specified, the userGroupingId must be a valid, non - empty guid!");
                }
                Guid.TryParse(Context.CurrentUser.FindFirst(GuestProjectIdClaim).Value, out var guestProjectId);
                Boolean.TryParse(Context.CurrentUser.FindFirst(IsGuestClaim).Value, out var isGuest);
                if (isGuest && (getUsersParams.UserGroupingType != UserGroupingTypeEnum.Project || getUsersParams.UserGroupingId != guestProjectId))
                {
                    return Response.Unauthorized("Unauthorized", "Missing Parameter Values", "GetUsersBasic: you must call get users with the project your a guest of!");
                }
                //if (IsGuest && (userGroupingType != UserGroupingTypeEnum.Project || userGroupingId != GuestProperties.ProjectId))
                
                if (isGuest && getUsersParams.UserGroupingType != UserGroupingTypeEnum.Project)
                {
                    return Response.Unauthorized("Unauthorized", "Missing Parameter Values", "GetUsersBasic: you must call get users with the project your a guest of!");
                }

                if (getUsersParams.UserGroupingType.Equals(UserGroupingTypeEnum.Project) && !getUsersParams.UserGroupingId.Equals(Guid.Empty))
                {
                    //TODO: Call Projects Microservice to get project level access result here. Currently hard coding to 1 (Success) - Yusuf
                    //Checks to see a user has direct read access to a project or has permissions to view all projects within their account.
                    //var resultCode = ValidProjectLevelAccess(userGroupingId.Value, DataTypeEnum.Project);
                    var resultCode = ResultCode.Success;
                    if(resultCode != ResultCode.Success)
                    {
                        return Response.BadRequest(resultCode.ToString(), "GetUsersBasic", resultCode.ToString());
                    }
                }

                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var userId);
                return await _userController.GetUsersBasicAsync(tenantId, userId, getUsersParams);

            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "GetUsersBasic threw an unhandled exception", ex);
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
                _logger.LogMessage(LogLevel.Error, "GetUserByIdBasic threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersForAccount(dynamic input)
        {
            GetUsersParams getUsersParams;
            try
            {
                getUsersParams = this.Bind<GetUsersParams>() ?? new GetUsersParams
                {
                    SearchValue = "",
                    PageNumber = 1,
                    PageSize = 10,
                    UserGroupingType = UserGroupingTypeEnum.None,
                    UserGroupingId = Guid.Empty,
                    ExcludeUsersInGroup = false,
                    OnlyCurrentUser = false,
                    IncludeInactive = false,
                    SortColumn = "FirstName",
                    SortOrder = DataSortOrder.Ascending,
                    IdpFilter = IdpFilterEnum.All
                };
                if (!getUsersParams.UserGroupingType.Equals(UserGroupingTypeEnum.None) && getUsersParams.UserGroupingId.Equals(Guid.Empty))
                {
                    return Response.BadRequest("Unable to get GetUsersForAccount", "Missing Parameter Values", "If the userGroupingType is specified, the userGroupingId must be a valid, non-empty guid!");
                }
                if (getUsersParams.UserGroupingType.Equals(UserGroupingTypeEnum.Project) && !getUsersParams.UserGroupingId.Equals(Guid.Empty))
                {
                    //TODO: Revisit to implement and validate project level access
                    //var resultCode = ValidProjectLevelAccess(userGroupingId.Value, DataTypeEnum.Project);
                    var resultCode = ResultCode.Success;
                    if (resultCode != ResultCode.Success)
                    {
                        return Response.BadRequest(resultCode.ToString(), "GetUsersForAccount", resultCode.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var currentUserId);
                return await _userController.GetUsersForAccount(getUsersParams, tenantId, currentUserId);
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

        private async Task<object> UpdateUserAsync(dynamic input)
        {
            Guid userId;
            User userModel;

            try
            {
                userId = input.id;
                userModel = this.Bind<User>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to update a User resource.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _userController.UpdateUserAsync(userId, userModel);
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
        private ResultCode ValidUserLevelAccess(Guid accessedUserId, PermissionEnum requiredPermission = PermissionEnum.CanViewUsers)
        {
            Guid.TryParse(Context.CurrentUser.FindFirst(UserIdClaim).Value, out var currentUserId);
            if (currentUserId == accessedUserId)
                return ResultCode.Success;

            if (accessedUserId == Guid.Empty || _userController.GetUserAsync(accessedUserId) == null)
                return ResultCode.RecordNotFound;
            //TODO: address the code once permission service dependency is implemented
            //var userPermissions = new Lazy<List<PermissionEnum>>(InitUserPermissionsList);

            //if (userPermissions.Value.Contains(requiredPermission) && AccountId == CollaborationService.GetAccountIdForUserId(accessedUserId).Payload)
            //    return ResultCode.Success;

            return ResultCode.Unauthorized;
        }
    }
}
