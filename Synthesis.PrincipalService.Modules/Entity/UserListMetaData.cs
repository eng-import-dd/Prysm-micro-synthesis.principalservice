using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Entity
{
    public class UserListMetaData : PagingMetaData
    {
        public List<User> Users { get; set; }
    }
}
