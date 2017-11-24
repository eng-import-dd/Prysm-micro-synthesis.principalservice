using Synthesis.Http.Configuration;

namespace Synthesis.PrincipalService
{
    public class HttpClientConfiguration : IHttpClientConfiguration
    {
        /// <inheritdoc />
        public bool TrustAllCerts => true;
    }
}
