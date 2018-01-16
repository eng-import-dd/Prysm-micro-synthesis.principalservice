using Synthesis.PrincipalService.Enums;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Responses
{
    public class GuestCreationResponse
    {
        public CreateGuestResponseCode ResultCode { get; set; }
        public User SynthesisUser { get; set; }
    }
}
