using System;
using System.Fabric;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Owin;
using Synthesis.Logging;

namespace Synthesis.PrincipalService
{
    internal class OwinCommunicationListener : ICommunicationListener
    {
        private readonly ILogger _logger;
        private readonly Action<IAppBuilder> _startup;
        private readonly ServiceContext _serviceContext;
        private readonly string _endpointName;
        private readonly string _appRoot;

        private IDisposable _webApp;
        private string _publishAddress;
        private string _listeningAddress;

        public OwinCommunicationListener(Action<IAppBuilder> startup, ServiceContext serviceContext, ILogger logger, string endpointName)
            : this(startup, serviceContext, logger, endpointName, null)
        {
        }

        public OwinCommunicationListener(Action<IAppBuilder> startup, ServiceContext serviceContext, ILogger logger, string endpointName, string appRoot)
        {
            if (startup == null)
            {
                throw new ArgumentNullException(nameof(startup));
            }

            if (serviceContext == null)
            {
                throw new ArgumentNullException(nameof(serviceContext));
            }

            if (endpointName == null)
            {
                throw new ArgumentNullException(nameof(endpointName));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(log4net));
            }

            _startup = startup;
            _serviceContext = serviceContext;
            _endpointName = endpointName;
            _logger = logger;
            _appRoot = appRoot;
        }

        public bool ListenOnSecondary { get; set; }

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var serviceEndpoint = _serviceContext.CodePackageActivationContext.GetEndpoint(_endpointName);
            int port = serviceEndpoint.Port;

            if (_serviceContext is StatefulServiceContext)
            {
                StatefulServiceContext statefulServiceContext = _serviceContext as StatefulServiceContext;

                _listeningAddress = string.Format(
                    CultureInfo.InvariantCulture,
                    "http://+:{0}/{1}{2}/{3}/{4}",
                    port,
                    string.IsNullOrWhiteSpace(_appRoot)
                        ? string.Empty
                        : _appRoot.TrimEnd('/') + '/',
                    statefulServiceContext.PartitionId,
                    statefulServiceContext.ReplicaId,
                    Guid.NewGuid());
            }
            else if (_serviceContext is StatelessServiceContext)
            {
                _listeningAddress = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}://+:{1}/{2}",
                    serviceEndpoint.Protocol.ToString().ToLower(),
                    port,
                    string.IsNullOrWhiteSpace(_appRoot)
                        ? string.Empty
                        : _appRoot.TrimEnd('/') + '/');
            }
            else
            {
                throw new InvalidOperationException();
            }

            _publishAddress = _listeningAddress.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);

            try
            {
                 _logger.Info($"Starting web server on {_listeningAddress}");

                _webApp = WebApp.Start(_listeningAddress, appBuilder => _startup.Invoke(appBuilder));

                _logger.Info($"Listening on {_publishAddress}");

                return Task.FromResult(_publishAddress);
            }
            catch (Exception ex)
            {
                _logger.Error($"Web server failed to open. ", ex);

                StopWebServer();

                throw;
            }
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Closing web server");

            StopWebServer();

            return Task.FromResult(true);
        }

        public void Abort()
        {
            _logger.Info("Aborting web server");

            StopWebServer();
        }

        private void StopWebServer()
        {
            if (_webApp != null)
            {
                try
                {
                    _webApp.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // no-op
                }
            }
        }
    }
}
