using Autofac;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService
{
    public class MicroserviceHttpClientResolver : IMicroserviceHttpClientResolver
    {
        private readonly ILifetimeScope _container;
        private readonly string _passThroughKey;
        private readonly string _serviceToServiceKey;

        public MicroserviceHttpClientResolver(ILifetimeScope container, string passThroughKey, string serviceToServiceKey)
        {
            _container = container;
            _passThroughKey = passThroughKey;
            _serviceToServiceKey = serviceToServiceKey;
        }

        /// <inheritdoc />
        public IMicroserviceHttpClient Resolve()
        {
            var canResolve = _container.TryResolve<IRequestHeaders>(out var requestHeaders) && requestHeaders.ContainsKey("Authorization");
            return _container.ResolveKeyed<IMicroserviceHttpClient>(canResolve ? _passThroughKey : _serviceToServiceKey);
        }
    }
}
