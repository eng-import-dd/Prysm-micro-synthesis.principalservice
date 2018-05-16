using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Email
{
    public interface IEmailSendingService
    {
        Task SendGuestVerificationEmailAsync(string firstName, string email, string redirect);
    }
}
