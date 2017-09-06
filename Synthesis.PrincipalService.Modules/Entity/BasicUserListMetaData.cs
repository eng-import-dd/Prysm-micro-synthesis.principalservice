using Synthesis.PrincipalService.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Entity
{
    public class BasicUserListMetaData : PagingMetaData
    {
        public List<UserBasicResponse> Users { get; set; }
    }
}
