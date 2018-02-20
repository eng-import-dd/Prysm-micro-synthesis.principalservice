using System;

namespace Synthesis.PrincipalService.Models
{
    public class UserNames
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public static UserNames Example()
        {
            return new UserNames()
            {
                FirstName = "First Name",
                LastName = "Last Name"
            };
        }
    }
}