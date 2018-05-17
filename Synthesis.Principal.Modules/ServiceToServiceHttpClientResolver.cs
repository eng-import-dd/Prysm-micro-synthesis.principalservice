using Autofac;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService
{
    public class ServiceToServiceHttpClientResolver : IMicroserviceHttpClientResolver
    {
        private readonly ILifetimeScope _container;
        private readonly string _serviceToServiceKey;

        public ServiceToServiceHttpClientResolver(ILifetimeScope container, string serviceToServiceKey)
        {
            _container = container;
            _serviceToServiceKey = serviceToServiceKey;
        }

        /// <inheritdoc />
        public IMicroserviceHttpClient Resolve()
        {
            return _container.ResolveKeyed<IMicroserviceHttpClient>(_serviceToServiceKey);
        }
    }
}
