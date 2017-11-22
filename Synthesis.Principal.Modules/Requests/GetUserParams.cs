using Synthesis.PrincipalService.Entity;

namespace Synthesis.PrincipalService.Requests
{
    public class GetUsersParams : ServerSidePagingParams
    {
        public IdpFilter IdpFilter { get; set; }
        public bool IncludeInactive { get; set; }
        public bool OnlyCurrentUser { get; set; }

        public static GetUsersParams Example()
        {
            return new GetUsersParams
            {
                OnlyCurrentUser = false,
                IncludeInactive = false,
                IdpFilter = IdpFilter.NotSet
            };
        }
    }

    public enum IdpFilter
    {
        All,
        IdpUsers,
        LocalUsers,
        NotSet
    }
}