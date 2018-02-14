using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Email api methods.
    /// </summary>
    public interface IEmailApi
    {
        /// <summary>
        /// Send user invite.
        /// </summary>
        /// <param name="userEmailRequestList"></param>
        /// <returns></returns>
        Task<List<UserEmailResponse>> SendUserInvite(List<UserEmailRequest> userEmailRequestList);

        /// <summary>
        /// Send welcome email.
        /// </summary>
        /// <param name="userEmailRequest"></param>
        /// <returns></returns>
        Task<bool> SendWelcomeEmail(UserEmailRequest userEmailRequest);

        /// <summary>
        /// Send user locked email.
        /// </summary>
        /// <param name="lockUserRequest"></param>
        /// <returns></returns>
        Task<bool> SendUserLockedMail(LockUserRequest lockUserRequest);

        /// <summary>
        /// Send reset password email.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<bool> SendResetPasswordEmail(PasswordResetEmailRequest request);
    }
}
