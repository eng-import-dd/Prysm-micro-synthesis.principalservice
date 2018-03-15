using AutoMapper;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.PrincipalService.InternalApi.Models;
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
