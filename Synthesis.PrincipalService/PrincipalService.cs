using System;
using System.Configuration;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Net;
using System.Net.Http;
using Synthesis.Logging.Log4Net;
using Synthesis.Logging;

namespace Synthesis.PrincipalService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance.
    /// </summary>
    internal sealed class PrincipalService : StatelessService
    {
        private readonly string _host = ConfigurationManager.AppSettings["Host"];
        private readonly string _protocol = ConfigurationManager.AppSettings["Protocol"];

        public PrincipalService(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            ILogger logger = new LoggerFactory().Get(new LogTopic("Synthesis.PrincipalService"));
            return new[]
            {
                new ServiceInstanceListener(serviceContext => new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, logger, "ServiceEndpoint", "principalservice"), "HTTP"),
                new ServiceInstanceListener(serviceContext => new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, logger, "SecureEndpoint", "principalservice"), "HTTPS")
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await ReportHealth(Context, cancellationToken);
        }

        private async Task ReportHealth(StatelessServiceContext context, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000);
                try
                {
                    var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri(new Uri($"{_protocol}://{_host}").ToString())
                    };

                    // Customize this route for the service
                    var response = httpClient.GetAsync("/principalservice//api/v1/principalservice/health").Result;
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        var info = new HealthInformation(context.NodeContext.NodeName, "PrincipalService-healthCheck", HealthState.Error)
                        {
                            Description = response.ReasonPhrase
                        };

                        Partition.ReportInstanceHealth(info);
                    }
                    else
                    {
                        var info = new HealthInformation(context.NodeContext.NodeName, "PrincipalService-healthCheck", HealthState.Ok);
                        Partition.ReportInstanceHealth(info);
                    }
                }
                catch (Exception exception)
                {
                    var info = new HealthInformation(context.NodeContext.NodeName, "PrincipalService-healthCheck", HealthState.Error)
                    {
                        Description = exception.Message
                    };

                    Partition.ReportInstanceHealth(info);
                }

            }
        }
    }
}
