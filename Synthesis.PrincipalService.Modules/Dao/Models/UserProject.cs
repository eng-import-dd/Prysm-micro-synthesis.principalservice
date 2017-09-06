using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.Nancy.MicroService.Entity;

namespace Synthesis.PrincipalService.Dao.Models
{
    public partial class UserProject
    {
        public Guid UserProjectId { get; set; }

        public Guid UserId { get; set; }

        public Guid ProjectId { get; set; }

        //public virtual Project Project { get; set; }

        public virtual SynthesisUser SynthesisUser { get; set; }
    }
}
