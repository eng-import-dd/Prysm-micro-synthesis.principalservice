using AutoMapper;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<UserRequest, User>();
            CreateMap<User, UserRequest>();
            CreateMap<UpdateUserRequest, User>();
            CreateMap<User, UserResponse>();
            CreateMap<User, BasicUserResponse>();
            CreateMap<PagingMetadata<User>, PagingMetadata<UserResponse>>();
            CreateMap<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>();
        }
    }
}
