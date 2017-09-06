using Synthesis.License.Manager.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synthesis.License.Manager.Interfaces
{
    /// <summary>
    /// Interface implemented by the License Service API.
    /// </summary>
    public interface ILicenseApi
    {
        /// <summary>
        /// Assigns a license to a user.
        /// </summary>
        /// <param name="licenseAssignment">DTO containing assignment information.</param>
        /// <returns>LicenseResponse containing licensing information.</returns>
        Task<LicenseResponse> AssignUserLicenseAsync(UserLicenseDto licenseAssignment);

        /// <summary>
        /// Refreshes purhcases from FNO
        /// </summary>
        /// <returns></returns>
        Task<bool> RefreshLicensesAsync(string tenantId);

        /// <summary>
        /// Gets a list of all license types assigned to a tenant.
        /// </summary>
        /// <param name="tenantId">ID of the tenant.</param>
        /// <returns>List of all license types available on the tenant.</returns>
        Task<List<LicenseType>> GetTenantUserLicenseTypesAsync(Guid tenantId);

        /// <summary>
        /// Lists all licenses with usage information for an tenant.
        /// </summary>
        /// <param name="tenantId">ID of the tenant.</param>
        /// <returns>LicenseResponse containing license usage information.</returns>
        Task<LicenseResponse> GetTenantLicenseDetailsAsync(Guid tenantId);

        /// <summary>
        /// Lists all licenses with usage information for an tenant.
        /// </summary>
        /// <param name="tenantId">ID of the tenant.</param>
        /// <returns>LicenseResponse containing license usage information.</returns>
        Task<List<LicenseSummaryDto>> GetTenantLicenseSummaryAsync(Guid tenantId);

        /// <summary>
        /// Removes a license from a user.
        /// </summary>
        /// <param name="licenseAssignment">DTO containing user and license information to remove.</param>
        /// <returns>LicenseResponse indicating if the license removal was successful.</returns>
        Task<LicenseResponse> ReleaseUserLicenseAsync(UserLicenseDto licenseAssignment);

        /// <summary>
        /// Gets detailed license information for the requested user.
        /// </summary>
        /// <param name="tenantId">ID of the tenant.</param>
        /// <param name="userId">ID of the user.</param>
        /// <returns>LicenseResponse containing license information for the user.</returns>
        Task<UserLicenseResponse> GetUserLicenseDetailsAsync(Guid tenantId, Guid userId);

        /// <summary>
        /// Method assigns a license type to all users for the tenant
        /// </summary>
        /// <returns></returns>
        Task<LicenseResponse> AssignLicenseToTenantUsersAsync(BulkLicenseDto dto);
    }
}