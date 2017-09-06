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
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Enums;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class UsersModule : NancyModule
    {
        private const string TenantIdClaim = "TenantId";
        private readonly IUsersController _userController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;
        private const string LegacyBaseRoute = "/api";
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

            Get("/v1/users/{id:guid}", GetUserAsync, null, "GetUser");
            Get("/api/v1/users/{id:guid}", GetUserAsync, null, "GetUserLegacy");

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

            _metadataRegistry.SetRouteMetadata("GetUser", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Get User",
                Description = "Retrieve a specific User resource."
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

        private async Task<object> CreateUserAsync(dynamic input)
        {
            UserRequest newUser;
            try
            {
                newUser = this.Bind<UserRequest>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                var result = await _userController.CreateUserAsync(newUser, tenantId);
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

        private async Task<object> GetUserAsync(dynamic input)
        {
            Guid id = input.id;

            try
            {
                return await _userController.GetUserAsync(id);
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
                _logger.Error($"Failed to get user with id {id} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }

        private async Task<object> GetUsersForAccount(dynamic input)
        {
            //Todo: check how the default values can be assigned
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
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a User resource", ex);
                return Response.BadRequestBindingException();
            }

            //Todo: Validation for all the parameters
            try
            {
                Guid.TryParse(Context.CurrentUser.FindFirst(TenantIdClaim).Value, out var tenantId);
                return await _userController.GetUsersForAccount(getUsersParams, tenantId);
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
    }
}
