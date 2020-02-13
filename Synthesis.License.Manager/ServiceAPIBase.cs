using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Synthesis.License.Manager.Exceptions;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Http;
using Synthesis.Http.Extensions;

namespace Synthesis.License.Manager
{
    /// <summary>
    /// Base class used to call remote restful web services.
    /// </summary>
    public class ServiceApiBase
    {
        private const string ApiErrorFormat = "API error occurred in {0} on line {1}\nResult code {2} returned from {3}\n{4}";
        private const string HttpErrorFormat = "HTTP response code is {0} for the request to {1}";

        private readonly IHttpClient _httpClient;

        protected ILogger LoggingService;
        protected string ApiBaseUrl;

        /// <summary>
        /// Security token to add as an authorization header.  By default an authorization header is not set.
        /// Override this property to provide a security token.
        /// </summary>
        protected virtual string SecurityToken => null;

        public ServiceApiBase(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        #region Generic GET, PUT, POST & DELETE Methods
        
        /// <summary>
        /// Performs an asynchronous get request.
        /// </summary>
        /// <typeparam name="T">Type of result being returned.</typeparam>
        /// <param name="route">Route to call.</param>
        /// <param name="callerMemberName"></param>
        /// <param name="callerLineNumber"></param>
        /// <returns>Specified generic type.</returns>
        protected virtual async Task<T> GetAsync<T>(string route, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1)
        {
            LogDebug("GET: " + route);

            try
            {
                using (var response = await _httpClient.GetWithJsonAsync(route, SecurityToken))
                {
                    return await HandleResponseAndResultCodes<T>(response, route, callerMemberName, callerLineNumber);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);

                throw;
            }
        }

        /// <summary>
        /// Performs an asynchronous post request.
        /// </summary>
        /// <typeparam name="T">Type of result being returned.</typeparam>
        /// <param name="route">Route to call.</param>
        /// <param name="postObject">Object to post with the request.</param>
        /// <param name="callerMemberName"></param>
        /// <param name="callerLineNumber"></param>
        /// <returns>Specified generic type.</returns>
        protected async Task<T> PostAsync<T>(string route, object postObject, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1)
        {
            LogDebug("POST: " + route);

            try
            {
                var jsonString = JsonConvert.SerializeObject(postObject, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                using (var stringContent = new StringContent(jsonString, Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PostWithJsonAsync(route, stringContent, SecurityToken))
                    {
                        return await HandleResponseAndResultCodes<T>(response, route, callerMemberName, callerLineNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);

                throw;
            }
        }

        /// <summary>
        /// Performs an asynchronous put request.
        /// </summary>
        /// <typeparam name="T">Type of result being returned.</typeparam>
        /// <param name="route">Route to call.</param>
        /// <param name="putObject">Object to post with the request.</param>
        /// <param name="callerMemberName"></param>
        /// <param name="callerLineNumber"></param>
        /// <returns></returns>
        protected async Task<T> PutAsync<T>(string route, object putObject, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1)
        {
            LogDebug("PUT: " + route);

            try
            {
                var jsonString = JsonConvert.SerializeObject(putObject, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                using (var stringContent = new StringContent(jsonString, Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PutWithJsonAsync(route, stringContent, SecurityToken))
                    {
                        return await HandleResponseAndResultCodes<T>(response, route, callerMemberName, callerLineNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);

                throw;
            }
        }

        /// <summary>
        /// Performs an asynchronous delete request.
        /// </summary>
        /// <typeparam name="T">Type of result being returned.</typeparam>
        /// <param name="route">Route to call.</param>
        /// <param name="callerMemberName"></param>
        /// <param name="callerLineNumber"></param>
        /// <returns>Specified generic type.</returns>
        protected async Task<T> DeleteAsync<T>(string route, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1)
        {
            LogDebug("DELETE: " + route);

            try
            {
                using (var response = await _httpClient.DeleteWithJsonAsync(route,SecurityToken))
                {
                    return await HandleResponseAndResultCodes<T>(response, route, callerMemberName, callerLineNumber);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);

                throw;
            }
        }

        private async Task<T> HandleResponseAndResultCodes<T>(HttpResponseMessage response, string route, string callerMemberName = "[Unknown]", int callerLineNumber = -1)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var apiErrorMessage = string.Format(HttpErrorFormat, response.StatusCode, route);
                throw new LicenseApiException(apiErrorMessage, "Unautorized error accessing LicenseAPI", ResultCode.Failed);
            }

            if (!response.IsSuccessStatusCode)
            {
                response.ReasonPhrase = string.Format(HttpErrorFormat, response.StatusCode, route);
                throw new InvalidOperationException(response.ReasonPhrase);
            }

            var result = JsonConvert.DeserializeObject<ServiceResult<T>>(await response.Content.ReadAsStringAsync());

            if (result.ResultCode != ResultCode.Success)
            {
                var apiErrorMessage = string.Format(ApiErrorFormat, callerMemberName, callerLineNumber, (int)result.ResultCode, route, result.Message);
                var apiException = new LicenseApiException(apiErrorMessage, result.Message, result.ResultCode);
                throw apiException;
            }

            return result.Payload;
        }
    

        #endregion

        #region Logging

        /// <summary>
        /// Logs a debug level message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        protected void LogDebug(String message)
        {
            LoggingService?.LogMessage(LogLevel.Debug, message);
        }

        /// <summary>
        /// Logs an Error level message.
        /// </summary>
        /// <param name="ex">Exception to log.</param>
        protected void LogError(Exception ex)
        {
            LoggingService?.LogMessage(LogLevel.Error, GetType().Name, ex);
        }

        #endregion
    }
}
