using Synthesis.PrincipalService.Enums;

namespace Synthesis.PrincipalService.Responses
{
    public class GuestCreationResponse
    {
        public CreateGuestResponseCode ResultCode { get; set; }
        public UserResponse SynthesisUser { get; set; }
    }
}
