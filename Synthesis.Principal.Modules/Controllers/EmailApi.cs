using System;
using Synthesis.Configuration;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Controllers
{
    /// <inheritdoc />
    public class EmailApi : IEmailApi
    {
        private readonly IMicroserviceHttpClientResolver _microserviceHttpClientResolver;
        private readonly string _serviceUrl;

        /// <inheritdoc />
        public EmailApi(IMicroserviceHttpClientResolver microserviceHttpClientResolver, IAppSettingsReader appSettingsReader)
        {
            _microserviceHttpClientResolver = microserviceHttpClientResolver;
            _serviceUrl = appSettingsReader.GetValue<string>("Email.Url");
        }

        /// <inheritdoc />
        public async Task<List<UserEmailResponse>> SendUserInvite(List<UserEmailRequest> request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var response = await microserviceHttpClient.PostAsync<List<UserEmailRequest>, List<UserEmailResponse>>($"{_serviceUrl}/v1/userinvites", request);
            if (response.ResponseCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception(response.ReasonPhrase);
            }
            return response.Payload;
        }

        /// <inheritdoc />
        public async Task<bool> SendWelcomeEmail(UserEmailRequest request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var response = await microserviceHttpClient.PostAsync<UserEmailRequest, bool>($"{_serviceUrl}/v1/sendwelcomeemail", request);
            if (response.ResponseCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception(response.ReasonPhrase);
            }
            return response.Payload;
        }

        /// <inheritdoc />
        public async Task<bool> SendUserLockedMail(LockUserRequest request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var response = await microserviceHttpClient.PostAsync<LockUserRequest, bool>($"{_serviceUrl}/v1/senduserlockedmail", request);
            if (response.ResponseCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception(response.ReasonPhrase);
            }
            return response.Payload;
        }

        /// <inheritdoc />
        public async Task<bool> SendResetPasswordEmail(PasswordResetEmailRequest request)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var response = await microserviceHttpClient.PostAsync<PasswordResetEmailRequest, bool>($"{_serviceUrl}/v1/sendresetpasswordemail", request);
            if (response.ResponseCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception(response.ReasonPhrase);
            }
            return response.Payload;
        }
    }
}
