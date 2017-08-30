using Synthesis.License.Manager.Models;

namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Response for a activation request
    /// </summary>
    public class ServerActivationResponse
    {
        public ServerActivationDTO Activation { get; set; }
        public ActivationResult Result { get; set; }
        public string Message { get; set; }
    }
}