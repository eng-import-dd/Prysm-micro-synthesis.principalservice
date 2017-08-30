using System.Collections.Generic;

namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Returns the history and current activation state for a license server
    /// </summary>
    public class ServerActivationHistoryResponse
    {
        public List<ServerActivationDTO> Activations { get; set; }
        public string Message { get; set; }
    }
}