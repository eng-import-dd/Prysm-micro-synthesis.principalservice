using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Utilities
{
    public interface IEmailUtility
    {
        Task<bool> SendGuestInvite(string projectName, string projectCode, string guestEmail, string @from);

        Task<bool> SendResetPasswordEmail(string email, string name, string link);

        Task<bool> SendVerifyAccountEmail(string firstName, string email, string accessCode, string emailVerificationId);

        Task<bool> SendHostEmail(string email, string userFullName, string userFirstName, string userEmail, string projectName);

        Task<bool> SendContent(IEnumerable<string> emailAddresses, IEnumerable<Attachment> attachments, string fromFullName);

        Task<bool> SendUserInvite(List<UserInviteResponse> newInvitedUsers);

        Task<bool> SendWelcomeEmail(string email, string firstname);
        Task<bool> SendUserLockedMail(List<User> orgAdmins, string userfullname, string useremail);
    }
}