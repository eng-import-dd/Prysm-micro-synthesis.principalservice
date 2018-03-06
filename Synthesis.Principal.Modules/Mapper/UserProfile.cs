using AutoMapper;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, BasicUserResponse>();
            CreateMap<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>();
            CreateMap<User, UserEmailRequest>();
        }
    }
}
