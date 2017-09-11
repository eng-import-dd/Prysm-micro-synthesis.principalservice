using AutoMapper;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserInviteProfile : Profile
    {
        public UserInviteProfile()
        {
            CreateMap<UserInviteRequest, UserInviteEntity>();
            CreateMap<UserInviteEntity, UserInviteResponse>();
            CreateMap<UserInviteEntity, UserInvite>();
        }
    }
}
