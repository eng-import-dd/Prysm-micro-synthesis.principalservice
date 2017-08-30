using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;

namespace Synthesis.License.Manager
{
    /// <summary>
    /// Base class used to call remote restful web services.
    /// </summary>
    public class ServiceAPIBase
    {
        private const string API_ERROR_FORMAT = "API error occurred in {0} on line {1}\nResult code {2} returned from {3}\n{4}";
        private const string HTTP_ERROR_FORMAT = "HTTP response code is {0} for the request to {1}";

        protected ILogger _loggingService;
        protected string _apiBaseUrl;

        /// <summary>
        /// Security token to add as an authorization header.  By default an authorization header is not set.
        /// Override this property to provide a security token.
        /// </summary>
        protected virtual string SecurityToken => null;

        #region Generic GET, PUT, POST & DELETE Methods

        private HttpClient _client;
        private HttpClient NewHttpClient()
        {
            if (_client == null)
            {
                _client = new HttpClient();
                if (SecurityToken != null)
                {
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                                                                                                SecurityToken);
                }
            }

            return _client;
        }

        /// <summary>
        /// Performs an asynchronous get request.
        /// </summary>
        /// <typeparam name="T">Type of result being returned.</typeparam>
        /// <param name="route">Route to call.</param>
        /// <param name="callerMemberName"></param>
        /// <param name="callerLineNumber"></param>
        /// <param name="useAsync">Flag indicating if the call should be made asynchronously.</param>
        /// <returns>Specified generic type.</returns>
        protected virtual async Task<T> GetAsync<T>(string route, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1, bool useAsync = true)
        {
            LogDebug("GET: " + route);

            try
            {
                var client = NewHttpClient();
                {
                    using (var response = useAsync ? await client.GetAsync(route) : client.GetAsync(route).Result)
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
        /// Performs an asynchronous post request.
        /// </summary>
        /// <typeparam name="T">Type of result being returned.</typeparam>
        /// <param name="route">Route to call.</param>
        /// <param name="postObject">Object to post with the request.</param>
        /// <param name="callerMemberName"></param>
        /// <param name="callerLineNumber"></param>
        /// <param name="useAsync">Flag indicating if the call should be made asynchronously.</param>
        /// <returns>Specified generic type.</returns>
        protected async Task<T> PostAsync<T>(string route, object postObject, [CallerMemberName] string callerMemberName = "[Unknown]", [CallerLineNumber] int callerLineNumber = -1, bool useAsync = true)
        {
            LogDebug("POST: " + route);

            try
            {
                var client = NewHttpClient();
                {
                    var jsonString = JsonConvert.SerializeObject(postObject, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                    using (var stringContent = new StringContent(jsonString, Encoding.UTF8, "application/json"))
                    {
                        using (var response = useAsync ? await client.PostAsync(route, stringContent) : client.PostAsync(route, stringContent).Result)
                        {
                            return await HandleResponseAndResultCodes<T>(response, route, callerMemberName, callerLineNumber);
                        }
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
                var client = NewHttpClient();
                {
                    var jsonString = JsonConvert.SerializeObject(putObject, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                    using (var stringContent = new StringContent(jsonString, Encoding.UTF8, "application/json"))
                    {
                        using (var response = await client.PutAsync(route, stringContent))
                        {
                            return await HandleResponseAndResultCodes<T>(response, route, callerMemberName, callerLineNumber);
                        }
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
                var client = NewHttpClient();
                {
                    using (var response = await client.DeleteAsync(route))
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

        private async Task<T> HandleResponseAndResultCodes<T>(HttpResponseMessage response, string route, string callerMemberName = "[Unknown]", int callerLineNumber = -1)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var apiErrorMessage = string.Format(HTTP_ERROR_FORMAT, response.StatusCode, route);
                throw new LicenseApiException(apiErrorMessage, "LicenseAPI", ResultCode.Unauthorized);
            }

            if (!response.IsSuccessStatusCode)
            {
                response.ReasonPhrase = string.Format(HTTP_ERROR_FORMAT, response.StatusCode, route);
                throw new HttpResponseException(response);
            }

            var result = await response.Content.ReadAsAsync<ServiceResult<T>>();

            if (result.ResultCode != ResultCode.Success)
            {
                var apiErrorMessage = string.Format(API_ERROR_FORMAT, callerMemberName, callerLineNumber, (int)result.ResultCode, route, result.Message);
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
            if (_loggingService != null)
                _loggingService.LogMessage(LogLevel.Debug, message);
        }

        /// <summary>
        /// Logs an Error level message.
        /// </summary>
        /// <param name="ex">Exception to log.</param>
        protected void LogError(Exception ex)
        {
            if (_loggingService != null)
                _loggingService.LogMessage(LogLevel.Error, GetType().Name, ex);
        }

        #endregion
    }
}
