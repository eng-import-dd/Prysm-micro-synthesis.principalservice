using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IEmailApi
    {
        Task<MicroserviceResponse<List<UserEmailResponse>>> SendUserInvite(List<UserEmailRequest> request);

        Task<MicroserviceResponse<bool>> SendWelcomeEmail(UserEmailRequest request);

        Task<MicroserviceResponse<bool>> SendUserLockedMail(LockUserRequest request);

        Task<MicroserviceResponse<bool>> SendResetPasswordEmail(PasswordResetEmailRequest request);
    }
}
