using AutoMapper;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Mapper
{
    /// <inheritdoc />
    /// <summary>
    /// Group Profile for auto mapper.
    /// </summary>
    /// <seealso cref="T:AutoMapper.Profile" />
    public class GroupProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupProfile"/> class.
        /// </summary>
        public GroupProfile()
        {
            CreateMap<CreateGroupRequest, Group>();
            CreateMap<Group, CreateGroupRequest>();
        }
    }
}
