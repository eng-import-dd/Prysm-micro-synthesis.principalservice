using Synthesis.Http.Microservice;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public class ProjectApi : IProjectApi
    {
        private readonly IMicroserviceHttpClient _microserviceHttpClient;
        private static string _serviceUrl;

        public ProjectApi(IMicroserviceHttpClient microserviceHttpClient)
        {
            _microserviceHttpClient = microserviceHttpClient;
            _serviceUrl = ConfigurationManager.AppSettings["ProjectService.Url"];
        }
        /// <inheritdoc />
        public Task<MicroserviceResponse<IEnumerable<Guid>>> GetUserIdsByProjectAsync(Guid projectId)
        {
            var get = string.Format(Routes.GetUserIdsByProjectFormat, projectId);
            return _microserviceHttpClient.GetAsync<IEnumerable<Guid>>($"{_serviceUrl}{get}");
        }

        private static class Routes
        {
            public static string GetUserIdsByProjectFormat => "/v1/projects/{0}/userids";
        }
    }
}
