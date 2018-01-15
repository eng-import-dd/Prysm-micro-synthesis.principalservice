using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.ApiWrappers
{
    public abstract class BaseApiWrapper
    {
        protected readonly IMicroserviceHttpClient HttpClient;
        protected readonly string ServiceUrl;

        protected BaseApiWrapper(IMicroserviceHttpClientResolver httpClientResolver, string serviceUrl)
        {
            HttpClient = httpClientResolver.Resolve();
            ServiceUrl = serviceUrl;
        }

        protected static bool IsSuccess(MicroserviceResponse response)
        {
            var code = response.ResponseCode;
            return (int)code >= 200
                && (int)code <= 299;
        }
    }
}
