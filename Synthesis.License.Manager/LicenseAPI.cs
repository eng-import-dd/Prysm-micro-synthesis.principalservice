using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Synthesis.License.Manager.Models;
using Synthesis.License.Manager.Exceptions;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Logging;

namespace Synthesis.License.Manager
{
    /// <summary>
    /// Implementation of ILicenseAPI used to communicate with the Licensing Service.
    /// </summary>
    public class LicenseApi : ServiceApiBase, ILicenseApi
    {
        private const string BaseRoute = "/api/v2/license";

        protected override string SecurityToken => "C35BB5D0-4A9D-4BFB-A3EC-6E24694D2B3D";

        public LicenseApi(ILogger loggingService)
        {
            LoggingService = loggingService;
            ApiBaseUrl = ConfigurationManager.AppSettings["BaseLicenseEndpoint"];
        }
        
        /// <summary>
        /// Method assigns a license type to all users for the account
        /// </summary>
        /// <returns></returns>
        public async Task<LicenseResponse> AssignLicenseToAccountUsersAsync(BulkLicenseDto dto)
        {
            return await PostAsync<LicenseResponse>(FormatRoute("bulk-licenses"), dto);
        }

        /// <summary>
        /// Assigns a license to a user.
        /// </summary>
        /// <param name="licenseAssignmentDto">DTO containing assignment information.</param>
        /// <returns>LicenseResponse containing licensing information.</returns>
        public async Task<LicenseResponse> AssignUserLicenseAsync(UserLicenseDto licenseAssignmentDto)
        {
            return await PostAsync<LicenseResponse>(FormatRoute("assignments"), licenseAssignmentDto);
        }
        
        /// <summary>
        /// Lists all licenses with usage information for an account.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <returns>LicenseResponse containing license usage information.</returns>
        public async Task<LicenseResponse> GetAccountLicenseDetailsAsync(Guid accountId)
        {
            return await GetAsync<LicenseResponse>(FormatRoute($"details/{accountId}"));
        }

        /// <summary>
        /// Lists all licenses with usage information for an account.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <returns>LicenseResponse containing license usage information.</returns>
        public async Task<List<LicenseSummaryDto>> GetAccountLicenseSummaryAsync(Guid accountId)
        {
            return await GetAsync<List<LicenseSummaryDto>>(FormatRoute($"summaries/{accountId}"));
        }

        /// <summary>
        /// Gets a list of all license types assigned to an account.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <returns>List of all license types available on the account.</returns>
        public async Task<List<LicenseType>> GetAccountUserLicenseTypesAsync(Guid accountId)
        {
            return await GetAsync<List<LicenseType>>(FormatRoute($"types/{accountId}"));
        }

        /// <summary>
        /// Gets detailed license information for the requested user.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <param name="userId">ID of the user.</param>
        /// <returns>LicenseResponse containing license information for the user.</returns>
        public async Task<UserLicenseResponse> GetUserLicenseDetailsAsync(Guid accountId, Guid userId)
        {
            return await GetAsync<UserLicenseResponse>(FormatRoute($"assignments/{accountId}/{userId}"));
        }

        /// <summary>
        /// Refreshes the license counts for an account
        /// </summary>
        /// <returns>LicenseResponse containing licensing information.</returns>
        public async Task<bool> RefreshLicensesAsync(string accountId)
        {
            return await PostAsync<bool>(FormatRoute("refresh"), accountId);
        }

        /// <summary>
        /// Removes a license from a user.
        /// </summary>
        /// <param name="licenseAssignmentDto">DTO containing user and license information to remove.</param>
        /// <returns>LicenseResponse indicating if the license removal was successful.</returns>
        public async Task<LicenseResponse> ReleaseUserLicenseAsync(UserLicenseDto licenseAssignmentDto)
        {
            return await PostAsync<LicenseResponse>(FormatRoute("releases"), licenseAssignmentDto);
        }

        protected override async Task<T> GetAsync<T>(string route, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1, bool useAsync = true)
        {
            try
            {
                return await base.GetAsync<T>(route);
            }
            catch (HttpRequestException httpEx)
            {
                LogError(httpEx);

                var innerExcpetion = httpEx.InnerException as System.Net.WebException;
                if (innerExcpetion != null && innerExcpetion.Status == System.Net.WebExceptionStatus.ConnectFailure)
                {
                    throw new LicenseApiException(innerExcpetion.Message, "Failed to connect to License Service", ResultCode.Failed);
                }

                throw;
            }
        }

        private string FormatRoute(string relativePath)
        {
            return $"{ApiBaseUrl}{BaseRoute}/{relativePath}";
        }
    }
}