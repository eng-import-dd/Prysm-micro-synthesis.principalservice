using System.Security.Claims;
using System.Threading.Tasks;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Dao;
using Synthesis.Nancy.MicroService.Entity;

namespace Synthesis.PrincipalService
{
    public class PrincipalServiceStatelessAuthorization : LegacyStatelessAuthorization
    {
        /// <inheritdoc />
        public PrincipalServiceStatelessAuthorization(ISynthesisMonolithicCloudDao synthesisMonolithicCloudDao, ILogger logger) : base(synthesisMonolithicCloudDao, logger)
        {
        }

        /// <inheritdoc />
        protected override Task<ClaimsIdentity> AddUserClaimsAsync(ClaimsIdentity identity, string token, TokenData tokenData)
        {
            // PLEASE READ:
            // The base class implementation of this method will make a call back to the monolith
            // to get the group permissions for the user and add those permissions as PERMISSION
            // claims. In some cases, this is not required and can be bypassed as we have done
            // below. If you want this functionality, you can either delete this entire override
            // or extend it by adding your own claims in addition to calling the base class
            // implementation.

            //return base.AddUserClaimsAsync(identity, token, tokenData);

            return Task.FromResult(identity);
        }
    }
}