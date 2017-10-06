using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Utilities
{
    public interface IEmailUtility
    {
        Task<bool> SendGuestInviteAsync(string projectName, string projectCode, string guestEmail, string @from);

        Task<bool> SendResetPasswordEmailAsync(string email, string name, string link);

        Task<bool> SendVerifyAccountEmailAsync(string firstName, string email, string accessCode, string emailVerificationId);

        Task<bool> SendHostEmailAsync(string email, string userFullName, string userFirstName, string userEmail, string projectName);

        Task<bool> SendContentAsync(IEnumerable<string> emailAddresses, IEnumerable<Attachment> attachments, string fromFullName);

        Task<bool> SendUserInviteAsync(List<UserInviteResponse> newInvitedUsers);

        Task<bool> SendWelcomeEmailAsync(string email, string firstname);
        Task<bool> SendUserLockedMailAsync(List<User> orgAdmins, string userfullname, string useremail);
    }
}