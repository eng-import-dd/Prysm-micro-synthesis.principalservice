using System;
using System.Threading.Tasks;
using Synthesis.Configuration;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Email
{
    public class EmailSendingService : IEmailSendingService
    {
        private readonly IEmailApi _emailApi;
        private readonly IAppSettingsReader _appSettingsReader;

        public EmailSendingService(IEmailApi emailApi, IAppSettingsReader appSettingsReader)
        {
            _emailApi = emailApi;
            _appSettingsReader = appSettingsReader;
        }

        public async Task<MicroserviceResponse> SendGuestVerificationEmailAsync(string firstName,
                                                                                string email,
                                                                                string redirect,
                                                                                Guid? verificationId)
        {
            var request = VerifyGuestEmail.BuildRequest(
                firstName,
                email,
                redirect,
                verificationId.ToString(),
                _appSettingsReader.GetValue<string>("WebClient.Url"));

            return await _emailApi.SendEmailAsync(request);
        }
    }
}
