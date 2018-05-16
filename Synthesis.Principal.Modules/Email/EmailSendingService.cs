using System;
using System.Threading.Tasks;
using Synthesis.EmailService.InternalApi.Api;

namespace Synthesis.PrincipalService.Email
{
    public class EmailSendingService : IEmailSendingService
    {
        private readonly IEmailApi _emailApi;

        public EmailSendingService(IEmailApi emailApi)
        {
            _emailApi = emailApi;
        }

        public async Task SendGuestVerificationEmailAsync(string firstName, string email, string redirect)
        {
            // TODO: Get the user info that currently lives in the policy_db.  That includes if the email is verified yet, when the last verification email was sent, and the verification token.

            // TODO: Grab the EmailVerificationId - this was previously gathered with _identityUserApi.GetTempTokenDataAsync()
            var emailVerificationId = Guid.NewGuid().ToString();   // NOTE: Added a temp ID for now until the EmailVerificationId TODO is handled

            // TODO: If the users email has already been verified, then throw an EmailAlreadyVerifiedException

            // TODO: If a verification email was sent less than a minute ago, then throw a EmailRecentlySentException

            var request = VerifyGuestEmail.BuildRequest(
                firstName,
                email,
                redirect,
                emailVerificationId);

            await _emailApi.SendEmailAsync(request);
        }
    }
}
