using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Dao.Models
{
    public class Group
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
