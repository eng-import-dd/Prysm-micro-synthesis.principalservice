using Nancy;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Modules
{
    public sealed class PrincipalServiceHealthModule : NancyModule
    {
        public PrincipalServiceHealthModule(IMetadataRegistry metadataRegistry)
        {
            // add some additional data for the documentation module
            metadataRegistry.SetRouteMetadata("HealthCheck", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Some informational message",
                Description = "Gets a synthesis user by id."
            });

            // create a health check endpoint
            Get("/v1/health", GetHealthAsync, null, "HealthCheck");
            Get("/api/v1/health", GetHealthAsync, null, "HealthCheckLegacy");
        }

        public async Task<object> GetHealthAsync(dynamic parameters)
        {
            // TODO: do some kind of health check if it passes return OK, otherwise 500
            await Task.Yield(); // Remove this when other async stuff happens.

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
        }

        internal PermissionEnum GetPermission(string value)
        {
            return (PermissionEnum)int.Parse(value);
        }
    }
}