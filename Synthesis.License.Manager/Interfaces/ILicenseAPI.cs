using Synthesis.License.Manager.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.License.Manager.Models;

namespace Synthesis.License.Manager.Interfaces
{
    /// <summary>
    /// Interface implemented by the License Service API.
    /// </summary>
    public interface ILicenseAPI
    {
        /// <summary>
        /// Assigns a license to a user.
        /// </summary>
        /// <param name="licenseAssignment">DTO containing assignment information.</param>
        /// <returns>LicenseResponse containing licensing information.</returns>
        Task<LicenseResponse> AssignUserLicense(UserLicenseDTO licenseAssignment);

        /// <summary>
        /// Refreshes purhcases from FNO
        /// </summary>
        /// <returns></returns>
        Task<bool> RefreshLicenses(string accountId);

        /// <summary>
        /// Gets a list of all license types assigned to an account.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <returns>List of all license types available on the account.</returns>
        Task<List<LicenseType>> GetAccountUserLicenseTypes(Guid accountId);

        /// <summary>
        /// Lists all licenses with usage information for an account.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <returns>LicenseResponse containing license usage information.</returns>
        Task<LicenseResponse> GetAccountLicenseDetails(Guid accountId);

        /// <summary>
        /// Lists all licenses with usage information for an account.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <returns>LicenseResponse containing license usage information.</returns>
        Task<List<LicenseSummaryDTO>> GetAccountLicenseSummary(Guid accountId);

        /// <summary>
        /// Removes a license from a user.
        /// </summary>
        /// <param name="licenseAssignment">DTO containing user and license information to remove.</param>
        /// <returns>LicenseResponse indicating if the license removal was successful.</returns>
        Task<LicenseResponse> ReleaseUserLicense(UserLicenseDTO licenseAssignment);

        /// <summary>
        /// Gets detailed license information for the requested user.
        /// </summary>
        /// <param name="accountId">ID of the account.</param>
        /// <param name="userId">ID of the user.</param>
        /// <returns>LicenseResponse containing license information for the user.</returns>
        Task<UserLicenseResponse> GetUserLicenseDetails(Guid accountId, Guid userId);

        /// <summary>
        /// Method assigns a license type to all users for the account
        /// </summary>
        /// <returns></returns>
        Task<LicenseResponse> AssignLicenseToAccountUsers(BulkLicenseDTO dto);

        /// <summary>
        /// Allocates a guest license for the account.
        /// </summary>
        Task<LicenseResponse> AllocateGuestLicense(string accountId);

        /// <summary>
        /// Releases a guest license for the account.
        /// </summary>
        Task<LicenseResponse> DeallocateGuestLicense(string accountId);

        Task<ServerActivationHistoryResponse> GetServerActivations();

        Task<ServerActivationResponse> ActivateServer(ServerActivationDTO activation);

        Task<ServerActivationStateResponse> GetServerActivationState();
    }
}