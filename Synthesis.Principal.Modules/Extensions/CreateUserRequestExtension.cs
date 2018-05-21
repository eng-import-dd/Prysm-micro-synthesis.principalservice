using System;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Extensions
{
    public static class CreateUserRequestExtension
    {
        /// <summary>
        /// Set the appropriate TenantId value on the C<see cref="CreateUserRequest"/> object. Can result in replacing a null value with an empty Guid, by design.
        /// </summary>
        /// <param name="createUserRequest">The model for creating a user.</param>
        /// <param name="JwtTenantId">The TenantId from the claims principal, which comes from the JWT.</param>
        /// <returns></returns>
        public static CreateUserRequest ReplaceNullOrEmptyTenantId(this CreateUserRequest createUserRequest, Guid JwtTenantId)
        {
            if (createUserRequest.TenantId == null || createUserRequest.TenantId == Guid.Empty)
            {
                createUserRequest.TenantId = JwtTenantId;
            }

            return createUserRequest;
        }
    }
}
