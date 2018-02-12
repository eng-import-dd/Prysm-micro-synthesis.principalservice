using AutoMapper;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserInviteProfile : Profile
    {
        public UserInviteProfile()
        {
            CreateMap<UserInviteRequest, UserInviteResponse>();
            CreateMap<UserInviteResponse, UserInvite>();
            CreateMap<UserInvite, UserInviteResponse>();
            CreateMap<UserInviteResponse, UserEmailRequest>();
            CreateMap<UserEmailResponse, UserInviteResponse>();
        }
    }
}
