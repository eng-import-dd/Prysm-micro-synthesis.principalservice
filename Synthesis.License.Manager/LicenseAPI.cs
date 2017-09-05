﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Synthesis.License.Manager.Models;
using Synthesis.License.Manager.Exceptions;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Logging;
using Synthesis.Http;

namespace Synthesis.License.Manager
{
    /// <inheritdoc cref="ILicenseApi" />
    /// <summary>
    /// Implementation of ILicenseAPI used to communicate with the Licensing Service.
    /// </summary>
    public class LicenseApi : ServiceApiBase, ILicenseApi
    {
        private const string BaseRoute = "/api/v2/license";

        protected override string SecurityToken => "C35BB5D0-4A9D-4BFB-A3EC-6E24694D2B3D";

        public LicenseApi(ILogger loggingService, IHttpClient httpClient): base(httpClient)
        {
            LoggingService = loggingService;
            ApiBaseUrl = ConfigurationManager.AppSettings["BaseLicenseEndpoint"];
        }
        
        /// <inheritdoc />
        public async Task<LicenseResponse> AssignLicenseToAccountUsersAsync(BulkLicenseDto dto)
        {
            return await PostAsync<LicenseResponse>(FormatRoute("bulk-licenses"), dto);
        }

        /// <inheritdoc />
        public async Task<LicenseResponse> AssignUserLicenseAsync(UserLicenseDto licenseAssignmentDto)
        {
            return await PostAsync<LicenseResponse>(FormatRoute("assignments"), licenseAssignmentDto);
        }

        /// <inheritdoc />
        public async Task<LicenseResponse> GetAccountLicenseDetailsAsync(Guid accountId)
        {
            return await GetAsync<LicenseResponse>(FormatRoute($"details/{accountId}"));
        }

        /// <inheritdoc />
        public async Task<List<LicenseSummaryDto>> GetAccountLicenseSummaryAsync(Guid accountId)
        {
            return await GetAsync<List<LicenseSummaryDto>>(FormatRoute($"summaries/{accountId}"));
        }

        /// <inheritdoc />
        public async Task<List<LicenseType>> GetAccountUserLicenseTypesAsync(Guid accountId)
        {
            return await GetAsync<List<LicenseType>>(FormatRoute($"types/{accountId}"));
        }

        /// <inheritdoc />
        public async Task<UserLicenseResponse> GetUserLicenseDetailsAsync(Guid accountId, Guid userId)
        {
            return await GetAsync<UserLicenseResponse>(FormatRoute($"assignments/{accountId}/{userId}"));
        }

        /// <inheritdoc />
        public async Task<bool> RefreshLicensesAsync(string accountId)
        {
            return await PostAsync<bool>(FormatRoute("refresh"), accountId);
        }

        /// <inheritdoc />
        public async Task<LicenseResponse> ReleaseUserLicenseAsync(UserLicenseDto licenseAssignmentDto)
        {
            return await PostAsync<LicenseResponse>(FormatRoute("releases"), licenseAssignmentDto);
        }

        protected override async Task<T> GetAsync<T>(string route, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1)
        {
            try
            {
                return await base.GetAsync<T>(route);
            }
            catch (HttpRequestException httpEx)
            {
                LogError(httpEx);

                if (httpEx.InnerException is WebException innerExcpetion && innerExcpetion.Status == WebExceptionStatus.ConnectFailure)
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