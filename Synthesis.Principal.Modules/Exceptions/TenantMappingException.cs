using System;

namespace Synthesis.PrincipalService.Exceptions
{
    public class TenantMappingException : Exception
    {
        public TenantMappingException(string message) : base(message)
        {
        }

        public TenantMappingException()
        {
        }
    }
}
