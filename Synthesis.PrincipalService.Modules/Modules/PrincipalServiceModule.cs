using System;
using FluentValidation;
using Nancy;
using Nancy.ModelBinding;
using Synthesis.EventBus;
using Synthesis.Nancy.MicroService.Extensions;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Validators;
using Synthesis.PrincipalService.Workflow.Controllers;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class PrincipalServiceModule : NancyModule
    {
        private readonly IPrincipalserviceController _principalserviceController;
        private readonly IValidator _principalserviceValidator;
        private readonly IEventService _eventService;
        private readonly IMetadataRegistry _metadataRegistry;

        public PrincipalServiceModule(IMetadataRegistry metadataRegistry, IValidatorLocator validatorLocator, IPrincipalserviceController principalserviceController, IEventService eventService) :
            base("/api/v1/principalservice")
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _principalserviceController = principalserviceController;

            // Init Validators
            _principalserviceValidator = validatorLocator.GetValidator(typeof(PrincipalserviceValidator));

            // Init Routes
            SetupRoute_Hello();

            SetupRoute_CreatePrincipalservice();
            SetupRoute_DeletePrincipalservice();
            SetupRoute_GetPrincipalservice();
            SetupRoute_UpdatePrincipalservice();
            _eventService = eventService;
        }

        private void SetupRoute_Hello()
        {
            //this.RequiresAuthentication();
            // add some additional data for the documentation module
            _metadataRegistry.SetRouteMetadata("Hello", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Some informational message",
                Description = "Gets a synthesis user by id."
            });

            // create a health check endpoint
            Get("/hello", _ =>
                          {
                              try
                              {
                                  /*
                                      this.RequiresClaims(
                                          c => c.Type == SynthesisStatelessAuthenticationConfiguration.PERMISSION_CLAIM_TYPE && GetPermission(c.Value) == PermissionEnum.CanLoginToAdminPortal);
                                  */
                              }
                              catch (Exception)
                              {
                                  return HttpStatusCode.Unauthorized;
                              }

                              // TODO: do some kind of health check if it passes return OK, otherwise 500
                              return new Response
                              {
                                  StatusCode = HttpStatusCode.OK,
                                  ReasonPhrase = "Hello World"
                              };
                          }, null, "Hello");
        }

        internal PermissionEnum GetPermission(string value)
        {
            return (PermissionEnum)int.Parse(value);
        }

        private void SetupRoute_GetPrincipalservice()
        {
            _metadataRegistry.SetRouteMetadata("GetPrincipalservice", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Get Principalservice",
                Description = "The route used to retrieve Principalservice."
            });

            Get("/GetPrincipalservice/{id:guid}", async input =>
                                         {
                                             Guid id = input.id;

                                             var validationResult = new Validation(_principalserviceValidator, id);

                                             if (!validationResult.Success)
                                             {
                                                 return Response.BadRequestValidationFailed(validationResult.Errors);
                                             }

                                             try
                                             {
                                                 var result = await _principalserviceController.GetPrincipalserviceAsync(id);

                                                 if (result.Code == DocumentDbCodes.NotFound.ToString())
                                                 {
                                                     return Response.NotFound(ResponseReasons.NotFoundPrincipalservice);
                                                 }

                                                 return result;
                                             }
                                             catch
                                             {
                                                 return Response.InternalServerError(ResponseReasons.InternalServerErrorDeletePrincipalservice);
                                             }
                                         }, null, "GetPrincipalservice");
        }

        private void SetupRoute_CreatePrincipalservice()
        {
            _metadataRegistry.SetRouteMetadata("CreatePrincipalservice", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Create a new Principalservice",
                Description = ""
            });

            Post("/CreatePrincipalservice}", async input =>
                                    {
                                        Principalservice newPrincipalservice;
                                        PrincipalserviceResponse createResponse;
                                        try
                                        {
                                            newPrincipalservice = this.Bind<Principalservice>();
                                        }
                                        catch
                                        {
                                            return Response.BadRequestBindingException();
                                        }

                                        //Validation
                                        var validationResult = new Validation(_principalserviceValidator, newPrincipalservice);

                                        if (!validationResult.Success)
                                        {
                                            return Response.BadRequestValidationFailed(validationResult.Errors);
                                        }

                                        try
                                        {
                                            createResponse = await _principalserviceController.CreatePrincipalserviceAsync(newPrincipalservice);
                                        }
                                        catch (Exception ex)
                                        {
                                            var msg = ex.Message;
                                            return Response.InternalServerError(ResponseReasons.InternalServerErrorCreatePrincipalservice);
                                        }

                                        if (createResponse == null)
                                        {
                                            return Response.InternalServerError(ResponseReasons.InternalServerErrorCreatePrincipalservice);
                                        }

                                        _eventService.Publish("CreatePrincipalservice");
                                        return createResponse;
                                    }, null, "CreatePrincipalservice");
        }

        private void SetupRoute_UpdatePrincipalservice()
        {
            _metadataRegistry.SetRouteMetadata("UpdatePrincipalservice", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Update Principalservice",
                Description = ""
            });

            Put("/UpdatePrincipalservice", async input =>
                                  {
                                      Principalservice updatePrincipalservice;
                                      try
                                      {
                                          updatePrincipalservice = this.Bind<Principalservice>();
                                      }
                                      catch
                                      {
                                          return Response.BadRequestBindingException();
                                      }

                                      //Validation
                                      var validationResult = new Validation(_principalserviceValidator, updatePrincipalservice);

                                      if (!validationResult.Success)
                                      {
                                          return Response.BadRequestValidationFailed(validationResult.Errors);
                                      }

                                      PrincipalserviceResponse result;
                                      try
                                      {
                                          result = await _principalserviceController.UpdatePrincipalserviceAsync(updatePrincipalservice);
                                          if (result.Code == DocumentDbCodes.NotFound.ToString())
                                          {
                                              return Response.NotFound(ResponseReasons.NotFoundPrincipalservice);
                                          }
                                      }
                                      catch
                                      {
                                          return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdatePrincipalservice);
                                      }

                                      _eventService.Publish("UpdatePrincipalservice");
                                      return result;
                                  }, null, "UpdatePrincipalservice");
        }

        private void SetupRoute_DeletePrincipalservice()
        {
            _metadataRegistry.SetRouteMetadata("DeletePrincipalservice", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Delete Principalservice",
                Description = "The route used to Delete Principalservice."
            });

            Get("/DeletePrincipalservice/{id:guid}", async input =>
                                            {
                                                Guid id = input.id;

                                                //Validation
                                                var validationResult = new Validation(_principalserviceValidator, id);

                                                if (!validationResult.Success)
                                                {
                                                    return Response.BadRequestValidationFailed(validationResult.Errors);
                                                }

                                                try
                                                {
                                                    var result = await _principalserviceController.DeletePrincipalserviceAsync(id);
                                                    if (result.Code == DocumentDbCodes.NotFound.ToString())
                                                    {
                                                        return Response.NotFound(ResponseReasons.NotFoundPrincipalservice);
                                                    }

                                                    _eventService.Publish("DeletePrincipalservice", id);

                                                    return result;
                                                }
                                                catch
                                                {
                                                    return Response.InternalServerError(ResponseReasons.InternalServerErrorDeletePrincipalservice);
                                                }
                                            }, null, "DeletePrincipalservice");
        }
    }
}
