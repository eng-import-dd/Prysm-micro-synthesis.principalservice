using Nancy;
using Nancy.Json;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Synthesis.Nancy.MicroService;

namespace Synthesis.PrincipalService.Modules
{
    /// <inheritdoc />
    /// <summary>
    /// Groups Module Class.
    /// </summary>
    /// <seealso cref="T:Nancy.NancyModule" />
    public class GroupsModule : AbstractModule
    {
        private const string TenantIdClaim = "TenantId";
        private const string UserIdClaim = "UserId";
        private readonly IGroupsController _groupsController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;
        private const string LegacyBaseRoute = "/api";
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private const string DeprecationWarning = "DEPRECATED";

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Synthesis.PrincipalService.Modules.GroupsModule" /> class.
        /// </summary>
        /// <param name="metadataRegistry">The metadata registry.</param>
        /// <param name="groupsController">The groups controller.</param>
        /// <param name="logger">The logger.</param>
        public GroupsModule(IMetadataRegistry metadataRegistry, IGroupsController groupsController, ILogger logger)
        {
            _metadataRegistry = metadataRegistry;
            _groupsController = groupsController;
            _logger = logger;

            this.RequiresAuthentication();

            SetupRouteMetadata();
            SetupRoute_CreateGroup();
            SetupRoute_GetGroupById();
            SetupRoute_GetGroupsForTenant();
            SetupRoute_DeleteGroup();
            SetupRuoute_UpdateGroup();
        }

        /// <summary>
        /// Setups the route metadata.
        /// </summary>
        private void SetupRouteMetadata()
        {
           _metadataRegistry.SetRouteMetadata("DeleteGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Delete Group",
                Description = "Delete a specific Group resource."
            });
        }

        /// <summary>
        /// Setups the route for create group.
        /// </summary>
        private void SetupRoute_CreateGroup()
        {
            const string path = "/v1/groups";
            Post(path, CreateGroupAsync, null, "CreateGroupAsync");
            Post(LegacyBaseRoute + path, CreateGroupAsync, null, "CreateGroupAsync");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = _serializer.Serialize(new Group());
            var metadataDescription = "Creates a new Group";

            _metadataRegistry.SetRouteMetadata("CreateGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("CreateGroupLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetGroupById()
        {
            const string path = "/v1/groups/{id}";
            Get(path, GetGroupByIdAsync, null, "GetGroupByIdAsync");
            Get(LegacyBaseRoute + path, GetGroupByIdAsync, null, "GetGroupByIdAsync");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = _serializer.Serialize(new Group());
            var metadataDescription = "Get Group By Id";

            _metadataRegistry.SetRouteMetadata("GetGroupById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetGroupByIdLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_GetGroupsForTenant()
        {
            const string path = "/v1/groups/tenant"; 
            Get(path, GetGroupsForTenantAsync, null, "GetGroupsForAccountAsync");
            Get(LegacyBaseRoute + path, GetGroupsForTenantAsync, null, "GetGroupsForAccountAsync");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = _serializer.Serialize(new Group());
            var metadataDescription = "Get Group for a tenant";

            _metadataRegistry.SetRouteMetadata("GetGroupsForAccountAsync", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("GetGroupsForAccountAsyncLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRoute_DeleteGroup()
        {
            const string path = "/v1/groups/{groupId}";
            Delete(path, DeleteGroupAsync, null, "DeleteGroupAsync");
            Delete(LegacyBaseRoute + path, DeleteGroupAsync, null, "DeleteGroupAsync");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataResponse = _serializer.Serialize(new Group());
            var metadataDescription = "Deletes a Group";

            _metadataRegistry.SetRouteMetadata("DeleteGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("DeleteGroupLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

        private void SetupRuoute_UpdateGroup()
        {
            const string path = "/v1/groups";
            Put(path, UpdateGroupAsync, null, "UpdateGroupAsync");
            Put(LegacyBaseRoute + path, UpdateGroupAsync, null, "UpdateGroupLegacy");

            // register metadata
            var metadataStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError };
            var metadataRequest = ToFormattedJson(new Group());
            var metadataResponse = ToFormattedJson(new Group());
            var metadataDescription = "Updates an existing Group";

            _metadataRegistry.SetRouteMetadata("UpdateGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = metadataDescription
            });

            _metadataRegistry.SetRouteMetadata("UpdateGroupLegacy", new SynthesisRouteMetadata
            {
                ValidStatusCodes = metadataStatusCodes,
                Request = metadataRequest,
                Response = metadataResponse,
                Description = $"{DeprecationWarning}: {metadataDescription}"
            });
        }

       /// <summary>
        /// Creates the group asynchronous.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>Group object.</returns>
        private async Task<object> CreateGroupAsync(dynamic input)
        {
            Group newGroup;
            try
            {
                newGroup = this.Bind<Group>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a Group resource", ex);
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
                _logger.Error("Failed to create group resource due to an error", ex);
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
                _logger.LogMessage(LogLevel.Error, "GetGroupByIdAsync threw an unhandled exception", ex);
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
                _logger.LogMessage(LogLevel.Error, "GetGroupsForTenantAsync threw an unhandled exception", ex);
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
                _logger.Warning("Group is either deleted or doesn't exist", ex);
                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Group is either deleted or doesn't exist"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create group resource due to an error", ex);
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
                _logger.Warning("Binding failed while attempting to update a Group resource", ex);
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
                _logger.Error("Failed to update group resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateUser);
            }
        }
    }
}
