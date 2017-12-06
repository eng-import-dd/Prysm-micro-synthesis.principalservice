using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Controllers
{
   public interface IProjectApi
   {
       Task<MicroserviceResponse<IEnumerable<Guid>>> GetUserIdsByProjectAsync(Guid projectId);
   }
}
