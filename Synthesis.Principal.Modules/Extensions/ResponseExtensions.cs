﻿using Nancy;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.PrincipalService.Constants;

namespace Synthesis.PrincipalService.Extensions
{
    public static class ResponseExtensions
    {
        /// <summary>
        /// A static method that returns a Response object with a 409 conflict http status code.  This method should be used when returning a custom reason phrase.
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
        /// A static method that returns a Response object with a 409 conflict http status code.  This method should be used when returning a custom reason phrase.
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