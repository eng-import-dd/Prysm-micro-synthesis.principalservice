//using System;
//using System.Collections.Generic;
//using System.Runtime.Serialization;
//using Microsoft.Hadoop.Avro;
//using Newtonsoft.Json;
//using Synthesis.PrincipalService.InternalApi.Models;
//using Synthesis.Serialization;

//namespace Synthesis.PrincipalService
//{
//    [DataContract(Name = "User")]
//    public class User
//    {
//        [DbMember]
//        [NullableSchema]
//        public string EmailDomain { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public Guid? CreatedBy { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public DateTime? CreatedDate { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public string Email { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public string FirstName { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public List<Guid> Groups { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        [JsonProperty("id")]
//        public Guid? Id { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public bool? IsIdpUser { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        public bool IsLocked { get; set; }

//        [DataMember]
//        [ApiMember]
//        [NullableSchema]
//        public List<string> IdpMappedGroups { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public DateTime? LastAccessDate { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        [NullableSchema]
//        public string LastName { get; set; }

//        [NullableSchema]
//        [ApiMember]
//        [DbMember]
//        [DataMember]
//        public string LdapId { get; set; }

//        [DataMember]
//        [ApiMember]
//        public LicenseType LicenseType { get; set; }

//        [DataMember]
//        [ApiMember]
//        [NullableSchema]
//        public string ProjectAccessCode { get; set; }

//        [DataMember]
//        [ApiMember]
//        [DbMember]
//        public string Username { get; set; }

//        public static User Example()
//        {
//            return new User();
//        }
//    }
//}