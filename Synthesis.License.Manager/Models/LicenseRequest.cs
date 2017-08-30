using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Represents a request for a license to assing to a user.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class LicenseRequest
    {
        /// <summary>
        /// Gets or sets the ID of the user.
        /// </summary>
        [DataMember]
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the account associated with the user.
        /// </summary>
        [DataMember]
        [Required]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Gets or sets the type of license being requested.
        /// </summary>
        [DataMember]
        public LicenseType LicenseType { get; set; }
    }
}
