using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class EmailNotInTenantDomainException : Exception
    {
        public EmailNotInTenantDomainException(string message) : base(message)
        {
        }
    }
}