using System;
using System.Linq;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Responses
{
    public class BasicUserResponse
    {
        public string FullName => $"{FirstName} {LastName}";
        public string Initials => $"{FirstName?.ToUpper().FirstOrDefault()}{LastName.ToUpper().FirstOrDefault()}";
        public string Email { get; set; }
        public string FirstName { get; set; }

        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public string LastName { get; set; }

        public static BasicUserResponse Example()
        {
            return new BasicUserResponse
            {
                Id = Guid.NewGuid(),
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                Email = "example@email.com"
            };
        }
    }
}