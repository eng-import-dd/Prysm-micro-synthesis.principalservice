using Synthesis.Nancy.MicroService.Entity;
using Synthesis.PrincipalService.Enums;

namespace Synthesis.PrincipalService.Responses
{
    public class ProvisionGuestUserResponse
    {
        public ProvisionGuestUserReturnCode ReturnCode { get; set; }
        public SynthesisUser User { get; set; }
    }
}
