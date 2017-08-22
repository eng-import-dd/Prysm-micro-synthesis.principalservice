using Owin;
using Synthesis.Tracking.Web;
using Microsoft.Owin.Cors;

namespace Synthesis.PrincipalService
{
    public static class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public static void ConfigureApp(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);
            app.Use(typeof(CorrelationScopeMiddleware));
            app.UseNancy(options =>
            {
                options.Bootstrapper = new PrincipalServiceBootstrapper();
            });
        }
    }
}
