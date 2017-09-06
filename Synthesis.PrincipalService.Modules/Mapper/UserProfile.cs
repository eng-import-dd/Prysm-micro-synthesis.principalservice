using AutoMapper;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Mapper
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<CreateUserRequest, User>();
            CreateMap<User, UserResponse>();
            CreateMap<PagingMetaData<User>, PagingMetaData<UserResponse>>();
        }
    }
}
