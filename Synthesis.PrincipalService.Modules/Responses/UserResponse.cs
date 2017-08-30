﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;

namespace Synthesis.PrincipalService.Responses
{
    public class UserResponse
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public Guid TenantId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public DateTime? LastLogin { get; set; }

        public string UserName { get; set; }

        public bool? IsLocked { get; set; }

        public int? PasswordAttempts { get; set; }

        public string LdapId { get; set; }

        public bool? IsIdpUser { get; set; }

        public LicenseType? LicenseType { get; set; }
        
        public DateTime? VerificationEmailSentDateTime { get; set; }

        public bool? IsEmailVerified { get; set; }

        public DateTime? EmailVerifiedDateTime { get; set; }

        public Guid? CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastAccessDate { get; set; }
    }
}
