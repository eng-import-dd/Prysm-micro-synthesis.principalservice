using AutoMapper;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, BasicUser>();
            CreateMap<PagingMetadata<User>, PagingMetadata<BasicUser>>();
            CreateMap<User, UserEmailRequest>();
        }
    }
}
