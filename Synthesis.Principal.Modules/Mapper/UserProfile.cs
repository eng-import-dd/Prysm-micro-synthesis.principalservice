using AutoMapper;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using User = Synthesis.PrincipalService.InternalApi.Models.User;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, User>();
            CreateMap<User, User>();
            CreateMap<User, User>();
            CreateMap<User, User>();
            CreateMap<User, BasicUserResponse>();
            CreateMap<PagingMetadata<User>, PagingMetadata<User>>();
            CreateMap<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>();
            CreateMap<User, UserEmailRequest>();
        }
    }
}
