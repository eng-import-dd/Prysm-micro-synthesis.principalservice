using System.Collections.Generic;
using Synthesis.PrincipalService.Dao.Models;

namespace Synthesis.PrincipalService.Entity
{
    public class UserListMetaData : PagingMetaData
    {
        public List<User> Users { get; set; }
    }
}
