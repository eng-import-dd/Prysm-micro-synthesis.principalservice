using Synthesis.Http.Configuration;
using Synthesis.PrincipalService.Utilities;

namespace Synthesis.PrincipalService.Configurations
{
    class HttpClientConfiguration : IHttpClientConfiguration
    {
        public bool TrustAllCerts => ConfigurationUtility.IsTrustAllCertificates();
    }
}
