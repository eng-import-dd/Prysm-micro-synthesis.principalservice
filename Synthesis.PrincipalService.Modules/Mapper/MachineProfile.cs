using AutoMapper;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
namespace Synthesis.PrincipalService.Mapper
{
    public class MachineProfile:Profile
    {
        public MachineProfile()
        {
            CreateMap<CreateMachineRequest, Machine>();
            CreateMap<Machine, MachineResponse>();
        }
    }
}
