using Microsoft.Owin.Cors;
using Owin;

namespace Synthesis.PrincipalService
{
    public static class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public static void ConfigureApp(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);

            // This will have the affect of registering all OwinMiddleware registered with the
            // root container (including CorrelationScopeMiddleware).
            app.UseAutofacMiddleware(PrincipalServiceBootstrapper.RootContainer);

            app.UseNancy(options =>
            {
                options.Bootstrapper = new PrincipalServiceBootstrapper();
            });
        }
    }
}
