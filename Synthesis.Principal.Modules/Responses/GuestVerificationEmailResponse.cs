using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Responses
{
    public class GuestVerificationEmailResponse : GuestVerificationEmailRequest
    {
        public bool MessageSentRecently { get; set; }
        public bool IsEmailVerified { get; set; }
    }
}
