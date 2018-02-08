using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Models
{
    [DataContract]
    public class User
    {
        // The EmailDomain property is a calculated value that we want to be persisted in documentdb.
        // We use JsonProperty attribute instead of DataMember attribute as Avro has trouble deserialzing
        // read only properties marked with DataMember attribute when the object is sent to Kafka.
        [JsonProperty]
        public string EmailDomain => Email?.Substring((int)Email?.IndexOf("@") + 1).ToLower();

        [DataMember]
        [NullableSchema]
        public Guid? CreatedBy { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? CreatedDate { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public List<Guid> Groups { get; set; }

        [DataMember]
        [NullableSchema]
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        [DataMember]
        [NullableSchema]
        public bool? IsIdpUser { get; set; }

        [DataMember]
        public bool IsLocked { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? LastAccessDate { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [NullableSchema]
        [DataMember]
        public string LdapId { get; set; }

        [DataMember]
        public string UserName { get; set; }

        public static User Example()
        {
            return new User
            {
                CreatedBy = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow,
                Email = "example@email.com",
                FirstName = "ExampleFirstname",
                Groups = new List<Guid> { Guid.NewGuid() },
                Id = Guid.NewGuid(),
                IsIdpUser = false,
                IsLocked = false,
                LastAccessDate = DateTime.UtcNow,
                LastName = "ExampleLastname",
                LdapId = "ExampleLdapId",
                UserName = "ExampleUsername"
            };
        }
    }
}