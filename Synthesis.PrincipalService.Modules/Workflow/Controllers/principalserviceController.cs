using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Synthesis.Configuration;
using Synthesis.PrincipalService.Dao;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public class PrincipalserviceController : IPrincipalserviceController
    {
        private readonly IBaseRepository<Principalservice> _principalserviceRepository;

        public PrincipalserviceController(IRepositoryFactory repositoryFactory, IAppSettingsReader appSettingsReader)
        {
            _principalserviceRepository = repositoryFactory.CreateRepository<Principalservice>(appSettingsReader);
        }

        public async Task<PrincipalserviceResponse> CreatePrincipalserviceAsync(Principalservice model)
        {
            var response = new PrincipalserviceResponse();
            try
            {
                model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
                model.CreatedDate = DateTime.UtcNow;
                var result = await _principalserviceRepository.CreateItemAsync(model);
                response.Principalservices = result;
                return response;
            }
            catch (DocumentClientException ex)
            {
                response.Code = ex.Error.Code;
            }

            return response;
        }

        public async Task<PrincipalserviceDeleteResponse> DeletePrincipalserviceAsync(Guid id)
        {
            var response = new PrincipalserviceDeleteResponse();
            try
            {
                await _principalserviceRepository.DeleteItemAsync(id.ToString());
            }
            catch (DocumentClientException ex)
            {
                response.Code = ex.Error.Code;
                response.IsDeleted = false;
            }
            catch (Exception)
            {
                response.IsDeleted = false;
            }
            return response;
        }

        public async Task<PrincipalserviceResponse> GetPrincipalserviceAsync(Guid id)
        {
            var response = new PrincipalserviceResponse();
            try
            {
                var result = await _principalserviceRepository.GetItemAsync(id.ToString());
                response.Principalservices = result;
                return response;
            }
            catch (DocumentClientException ex)
            {
                response.Code = ex.Error.Code;
            }

            return response;
        }

        public async Task<PrincipalserviceResponse> UpdatePrincipalserviceAsync(Principalservice model)
        {
            var response = new PrincipalserviceResponse();
            try
            {
                model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
                model.CreatedDate = DateTime.UtcNow;
                var result = await _principalserviceRepository.UpdateItemAsync(model.Id.ToString(), model);
                response.Principalservices = result;
                return response;
            }
            catch (DocumentClientException ex)
            {
                response.Code = ex.Error.Code;
            }

            return response;
        }
    }
}
