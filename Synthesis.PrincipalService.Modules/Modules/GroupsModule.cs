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
using Synthesis.Nancy.MicroService;

namespace Synthesis.PrincipalService.Modules
{
    /// <inheritdoc />
    /// <summary>
    /// Groups Module Class.
    /// </summary>
    /// <seealso cref="T:Nancy.NancyModule" />
    public class GroupsModule : NancyModule
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
        }

        /// <summary>
        /// Setups the route metadata.
        /// </summary>
        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("UpdateGroup", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Update Group",
                Description = "Update a specific Group resource."
            });

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
                //TODO: Call Accounts Microservice to get Account level access result here. Currently hard coding to 1 (Success) - Yusuf
                //var resultCode = ValidAccountLevelAccess(groupId, DataTypeEnum.Group, result.Payload.AccountId);

                var resultCode = HttpStatusCode.OK;
                if (resultCode != HttpStatusCode.OK)
                {
                    return Response.Unauthorized("Unauthorized", HttpStatusCode.Unauthorized.ToString(), "GetGroupById: No valid account level access to groups!");
                }

                return await _groupsController.GetGroupByIdAsync(groupId);
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
                _logger.LogMessage(LogLevel.Error, "GetGroupByIdAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetUser);
            }
        }
    }
}
