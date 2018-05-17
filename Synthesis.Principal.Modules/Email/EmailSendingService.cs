using System;
using System.Net;
using System.Threading.Tasks;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.PrincipalService.Exceptions;

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
            // TODO: Get the user info that used to live in the policy_db and was moved to Cosmos DB in the User model.  That includes if the email is verified yet, when the last verification email was sent, and the verification token.

            // TODO: Grab the EmailVerificationId - this was previously gathered with _identityUserApi.GetTempTokenDataAsync()
            var emailVerificationId = Guid.NewGuid().ToString();   // NOTE: Added a temp ID for now until the EmailVerificationId TODO is handled

            // TODO: If the users email has already been verified, then throw an EmailAlreadyVerifiedException

            // TODO: If a verification email was sent less than a minute ago, then throw a EmailRecentlySentException

            var request = VerifyGuestEmail.BuildRequest(
                firstName,
                email,
                redirect,
                emailVerificationId);

            var result = await _emailApi.SendEmailAsync(request);
            if (result.ResponseCode != HttpStatusCode.OK)
            {
                throw new SendEmailException($"Email did not send due to an error. ReasonPhrase={result.ReasonPhrase} ErrorResponse={result.ErrorResponse}");
            }
        }
    }
}
