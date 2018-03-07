using AutoMapper;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserInviteProfile : Profile
    {
        public UserInviteProfile()
        {
            CreateMap<UserInvite, UserEmailRequest>();
            CreateMap<UserEmailResponse, UserInvite>();
        }
    }
}
