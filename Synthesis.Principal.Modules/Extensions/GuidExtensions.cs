using System;

namespace Synthesis.PrincipalService.Extensions
{
    public static class GuidExtensions
    {
        public static Guid ToGuid(this Guid? guid)
        {
            return guid ?? default(Guid);
        }
    }
}
