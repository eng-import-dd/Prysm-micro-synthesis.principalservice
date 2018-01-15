using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.ApiWrappers.Interfaces
{
    public interface IProjectApiWrapper
    {
        Task<MicroserviceResponse<Project>> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<MicroserviceResponse<Project>> GetProjectByIdAsync(Guid projectId);
    }
}
