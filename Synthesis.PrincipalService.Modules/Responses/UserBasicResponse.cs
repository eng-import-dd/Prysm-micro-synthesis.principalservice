using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Responses
{
    public class UserBasicResponse
    {

        public Guid Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string FullName { get { return string.Format("{0} {1}", FirstName, LastName); } }
        public string Initials { get { return string.Format("{0}{1}", FirstName.ToUpper().FirstOrDefault(), LastName.ToUpper().FirstOrDefault()); } }

        public string Email { get; set; }

        public static explicit operator UserBasicResponse(Task<List<UserBasicResponse>> v)
        {
            throw new NotImplementedException();
        }
    }
}
