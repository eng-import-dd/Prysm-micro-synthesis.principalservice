using System;
using Synthesis.License.Manager.Models;

namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Contains the information required to activate a server
    /// </summary>
    public class ServerActivationDTO
    {
        /// <summary>
        /// Id that is registered in FNO to for a server activation
        /// </summary>
        public string ActivationId { get; set; }

        /// <summary>
        /// Account Id associated with activation
        /// </summary>
        public Guid AccountId { get; set; }

        /// <summary>
        /// File that contains an FNO capability response so a client can register without needing call FNO
        /// </summary>
        public byte[] ActivationFile { get; set; }

        /// <summary>
        /// Type of activation / file or key
        /// </summary>
        public ActivationType ActivationType { get; set; }

        /// <summary>
        /// Date server is activated
        /// </summary>
        public DateTime ActivationDate { get; set; }

        /// <summary>
        /// Date activation expires
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Date server is activated
        /// </summary>
        public string ActivatingUserId { get; set; }
    }
}