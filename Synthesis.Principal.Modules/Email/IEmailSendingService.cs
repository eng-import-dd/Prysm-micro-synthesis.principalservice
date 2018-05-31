using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Email
{
    public interface IEmailSendingService
    {
        Task<MicroserviceResponse> SendGuestVerificationEmailAsync(string firstName, string email, string redirect, Guid? verificationId);
    }
}
