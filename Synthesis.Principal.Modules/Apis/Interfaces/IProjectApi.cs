using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Apis.Interfaces
{
    public interface IProjectApi
    {
        Task<MicroserviceResponse<Project>> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<MicroserviceResponse<Project>> GetProjectByIdAsync(Guid projectId);
    }
}
