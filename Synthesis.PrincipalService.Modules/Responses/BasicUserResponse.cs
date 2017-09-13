using System;
using Newtonsoft.Json;
using System.Linq;

namespace Synthesis.PrincipalService.Responses
{
    public class BasicUserResponse
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string FullName { get { return string.Format("{0} {1}", FirstName, LastName); } }

        public string Initials { get { return string.Format("{0}{1}", FirstName?.ToUpper().FirstOrDefault(), LastName.ToUpper().FirstOrDefault()); } }

    }
}
