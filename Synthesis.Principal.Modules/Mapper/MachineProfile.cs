using AutoMapper;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;
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
            CreateMap<UpdateMachineRequest, Machine>();
        }
    }
}
