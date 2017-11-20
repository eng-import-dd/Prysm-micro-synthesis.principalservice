using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Authentication;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Controllers.Interfaces;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class PrincipalsModule : SynthesisModule
    {
        private readonly IPrincipalsController _principalsController;

        public PrincipalsModule(
            IPrincipalsController principalsController,
            IMetadataRegistry metadataRegistry,
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory)
            : base(PrincipalServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            _principalsController = principalsController;

            this.RequiresAuthentication();

            // CRUD routes
            CreateRoute("CreatePrincipal", HttpMethod.Post, "/v1/principals", CreatePrincipalAsync)
                .Description("Create a new Principal resource")
                .StatusCodes(HttpStatusCode.Created, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError)
                .RequestFormat(new Principal
                {
                    Name = "Testing"
                })
                .ResponseFormat(new Principal
                {
                    Id = Guid.NewGuid(),
                    Name = "Testing",
                    CreatedDate = DateTime.UtcNow,
                    LastAccessDate = DateTime.UtcNow
                });

            CreateRoute("GetPrincipal", HttpMethod.Get, "/v1/principals/{id:guid}", GetPrincipalAsync)
                .Description("Get a Principal resource by it's identifier.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("UpdatePrincipal", HttpMethod.Put, "/v1/principals/{id:guid}", UpdatePrincipalAsync)
                .Description("Update a Principal resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

            CreateRoute("DeletePrincipal", HttpMethod.Delete, "/v1/principals/{id:guid}", DeletePrincipalAsync)
                .Description("Delete a Principal resource.")
                .StatusCodes(HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
        }

        private async Task<object> CreatePrincipalAsync(dynamic input)
        {
            Principal newPrincipal;
            try
            {
                newPrincipal = this.Bind<Principal>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a Principal resource", ex);
                return Response.BadRequestBindingException();
            }

            // Make sure this is called outside of our typical try/catch blocks because this will
            // throw a special Nancy exception that will result in a 401 or 403 status code.
            // If we were to put this in our try/catch below, it would come across as a 500
            // instead, which is inaccurate.
            await RequiresAccess()
                .WithProjectIdExpansion(ctx => newPrincipal.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                var result = await _principalsController.CreatePrincipalAsync(newPrincipal);
                return Negotiate
                    .WithModel(result)
                    .WithStatusCode(HttpStatusCode.Created);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Validation failed while attempting to create a Principal resource", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create principal resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreatePrincipal);
            }
        }

        private async Task<object> GetPrincipalAsync(dynamic input)
        {
            Guid id = input.id;
            Principal result;

            try
            {
                result = await _principalsController.GetPrincipalAsync(id);
            }
            catch (NotFoundException)
            {
                Logger.Error($"Could not find a '{id}'");
                return Response.NotFound(ResponseReasons.NotFoundPrincipal);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error($"Validation failed while attempting to get '{id}'", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get '{id}'", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetPrincipal);
            }

            // As an optimization we're getting the resource from the database first so we can use
            // the ProjectId from the resource in the expansion below (with out re-getting it).
            await RequiresAccess()
                .WithProjectIdExpansion(ctx => result.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            return result;
        }

        private async Task<object> UpdatePrincipalAsync(dynamic input)
        {
            Guid id = input.id;
            Principal principalModel;

            try
            {
                principalModel = this.Bind<Principal>();
            }
            catch (Exception ex)
            {
                Logger.Warning("Binding failed while attempting to update a Principal resource.", ex);
                return Response.BadRequestBindingException();
            }

            await RequiresAccess()
                .WithProjectIdExpansion(async (ctx, ct) =>
                {
                    var resource = await _principalsController.GetPrincipalAsync(id);
                    return resource.ProjectId;
                })
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _principalsController.UpdatePrincipalAsync(id, principalModel);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error($"Validation failed while attempting to update '{id}'", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException)
            {
                Logger.Error($"Could not find '{id}'");
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdatePrincipal);
            }
            catch (Exception ex)
            {
                Logger.Error($"Unhandled exception encountered while attempting to update '{id}'", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdatePrincipal);
            }
        }

        private async Task<object> DeletePrincipalAsync(dynamic input)
        {
            Guid id = input.id;

            await RequiresAccess()
                .WithProjectIdExpansion(async (ctx, ct) =>
                {
                    var resource = await _principalsController.GetPrincipalAsync(id);
                    return resource.ProjectId;
                })
                .ExecuteAsync(CancellationToken.None);

            try
            {
                await _principalsController.DeletePrincipalAsync(id);

                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Resource has been deleted"
                };
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error($"Validation failed while attempting to delete '{id}'", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Unhandled exception encountered while attempting to delete '{id}'", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorDeletePrincipal);
            }
        }
    }
}
