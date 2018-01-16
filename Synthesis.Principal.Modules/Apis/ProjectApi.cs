using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Apis.Interfaces;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Apis
{
    public class ProjectApi : BaseApi, IProjectApi
    {
        public ProjectApi(IMicroserviceHttpClientResolver httpClient, string serviceUrl) : base(httpClient, serviceUrl)
        {
        }

        public async Task<MicroserviceResponse<Project>> GetProjectByAccessCodeAsync(string projectAccessCode)
        {
            return await HttpClient.GetAsync<Project>($"{ServiceUrl}/v1/projects/{projectAccessCode}");
        }

        public async Task<MicroserviceResponse<Project>> GetProjectByIdAsync(Guid projectId)
        {
            return await HttpClient.GetAsync<Project>($"{ServiceUrl}/v1/projects/{projectId}");
        }
    }
}
