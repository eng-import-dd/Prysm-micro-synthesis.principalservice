using Synthesis.Configuration;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public class EmailApi : IEmailApi
    {
        private readonly IMicroserviceHttpClientResolver _microserviceHttpClientResolver;
        private readonly string _serviceUrl;

        public EmailApi(IMicroserviceHttpClientResolver microserviceHttpClientResolver, IAppSettingsReader appSettingsReader)
        {
            _microserviceHttpClientResolver = microserviceHttpClientResolver;
            _serviceUrl = appSettingsReader.GetValue<string>("Email.Url");
        }

        public async Task<MicroserviceResponse<List<UserEmailResponse>>> SendUserInvite(List<UserEmailRequest> request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            return await microserviceHttpClient.PostAsync<List<UserEmailRequest>, List<UserEmailResponse>>($"{_serviceUrl}/v1/userinvites", request);
        }

        public async Task<MicroserviceResponse<bool>> SendWelcomeEmail(UserEmailRequest request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            return await microserviceHttpClient.PostAsync<UserEmailRequest, bool>($"{_serviceUrl}/v1/sendwelcomeemail", request);
        }

        public async Task<MicroserviceResponse<bool>> SendUserLockedMail(LockUserRequest request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            return await microserviceHttpClient.PostAsync<LockUserRequest, bool>($"{_serviceUrl}/v1/senduserlockedmail", request);
        }

        public async Task<MicroserviceResponse<bool>> SendResetPasswordEmail(PasswordResetEmailRequest request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            return await microserviceHttpClient.PostAsync<PasswordResetEmailRequest, bool>($"{_serviceUrl}/v1/sendresetpasswordemail", request);
        }
    }
}
