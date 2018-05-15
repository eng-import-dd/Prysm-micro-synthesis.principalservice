using Nancy;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.InternalApi.Constants;

namespace Synthesis.PrincipalService.Extensions
{
    public static class ResponseExtensions
    {
        /// <summary>
        /// A static method that returns a Response object with a 409 conflict http status code. This method should be used when returning a custom reason phrase.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="reasonPhrase"></param>
        /// <returns>Nancy.Response</returns>
        public static Response UserExists(this IResponseFormatter formatter, string reasonPhrase)
        {
            var response = FormatResponse(formatter, ErrorCodes.UserExists, ErrorMessages.UserExists);
            response.ReasonPhrase = reasonPhrase;
            response.StatusCode = HttpStatusCode.Conflict;
            return response;
        }

        /// <summary>
        /// A static method that returns a Response object with a 409 conflict http status code. This method should be used when returning a custom reason phrase.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="reasonPhrase"></param>
        /// <returns>Nancy.Response</returns>
        public static Response UserNotInvited(this IResponseFormatter formatter, string reasonPhrase)
        {
            var response = FormatResponse(formatter, ErrorCodes.UserNotInvited, ErrorMessages.UserNotInvited);
            response.ReasonPhrase = reasonPhrase;
            response.StatusCode = HttpStatusCode.Conflict;
            return response;
        }

        /// <summary>
        /// A static method that returns a Response object with a 424 failed dependency http status code. This method should be used when returning a custom reason phrase.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="reasonPhrase"></param>
        /// <returns>Nancy.Response</returns>
        public static Response TenantMappingFailed(this IResponseFormatter formatter, string reasonPhrase)
        {
            var response = FormatResponse(formatter, ErrorCodes.TenantMappingFailed, ErrorMessages.TenantMappingFailed);
            response.ReasonPhrase = reasonPhrase;
            response.StatusCode = HttpStatusCode.FailedDependency;
            return response;
        }

        /// <summary>
        /// A static method that returns a Response object with a 424 failed dependency http status code. This method should be used when returning a custom reason phrase.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="reasonPhrase"></param>
        /// <returns>Nancy.Response</returns>
        public static Response SetPasswordFailed(this IResponseFormatter formatter, string reasonPhrase)
        {
            var response = FormatResponse(formatter, ErrorCodes.SetPasswordFailed, ErrorMessages.SetPasswordFailed);
            response.ReasonPhrase = reasonPhrase;
            response.StatusCode = HttpStatusCode.FailedDependency;
            return response;
        }

        /// <summary>
        /// A static method that returns a Response object with a 424 failed dependency http status code. This method should be used when returning a custom reason phrase.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="reasonPhrase"></param>
        /// <returns>Nancy.Response</returns>
        public static Response EmailAlreadyVerified(this IResponseFormatter formatter, string reasonPhrase)
        {
            var response = FormatResponse(formatter, /*ErrorCodes.EmailAlreadyVerified*/"EmailAlreadyVerified", ErrorMessages.EmailAlreadyVerified);
            response.ReasonPhrase = reasonPhrase;
            response.StatusCode = HttpStatusCode.FailedDependency;
            return response;
        }

        /// <summary>
        /// A static method that returns a Response object with a 424 failed dependency http status code. This method should be used when returning a custom reason phrase.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="reasonPhrase"></param>
        /// <returns>Nancy.Response</returns>
        public static Response EmailRecentlySent(this IResponseFormatter formatter, string reasonPhrase)
        {
            var response = FormatResponse(formatter, /*ErrorCodes.EmailRecentlySent*/"EmailRecentlySent", ErrorMessages.EmailRecentlySent);
            response.ReasonPhrase = reasonPhrase;
            response.StatusCode = HttpStatusCode.FailedDependency;
            return response;
        }

        private static Response FormatResponse(this IResponseFormatter formatter, string responseCode, string errorMessage)
        {
            return formatter.AsJson(new FailedResponse
            {
                Code = responseCode,
                Message = errorMessage
            });
        }
    }
}
