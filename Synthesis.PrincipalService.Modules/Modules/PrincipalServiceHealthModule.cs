using System;
using Nancy;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class PrincipalServiceHealthModule : NancyModule
    {
        public PrincipalServiceHealthModule(IMetadataRegistry metadataRegistry) :
            base("/api/v1/principalservice")
        {
            // add some additional data for the documentation module
            metadataRegistry.SetRouteMetadata("HealthCheck", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Some informational message",
                Description = "Gets a synthesis user by id."
            });
            // create a health check endpoint
            Get("/health", (_) =>
            {
                // TODO: do some kind of health check if it passes return OK, otherwise 500
                if (true)
                {
                    return "All is Well";
                }
                //else
                //{
                //    return new Response()
                //    {
                //        StatusCode = HttpStatusCode.InternalServerError,
                //        ReasonPhrase = "Something is borked"
                //    };
                //}
            }, null, "HealthCheck");
        }

        internal PermissionEnum GetPermission(string value)
        {
            return ((PermissionEnum)Int32.Parse(value));
        }
    }
}
