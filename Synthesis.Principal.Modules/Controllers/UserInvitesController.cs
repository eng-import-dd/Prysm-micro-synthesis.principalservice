using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using Synthesis.DocumentStorage;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;

namespace Synthesis.PrincipalService.Controllers
{
    public class UserInvitesController : IUserInvitesController
    {
        private readonly IRepository<UserInvite> _userInviteRepository;
        private readonly IRepository<User> _userRepository;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly IValidator _tenantIdValidator;
        private readonly ITenantApi _tenantApi;
        private readonly IEmailApi _emailApi;
        private readonly IValidatorLocator _validatorLocator;

        public UserInvitesController(
            IRepositoryFactory repositoryFactory,
            ILoggerFactory loggerFactory,
            IMapper mapper,
            IValidatorLocator validatorLocator,
            ITenantApi tenantApi,
            IEmailApi emailApi)
        {
            _userInviteRepository = repositoryFactory.CreateRepository<UserInvite>();
            _userRepository = repositoryFactory.CreateRepository<User>();
            _logger = loggerFactory.GetLogger(this);
            _mapper = mapper;
            _tenantIdValidator = validatorLocator.GetValidator(typeof(TenantIdValidator));
            _tenantApi = tenantApi;
            _emailApi = emailApi;
            _validatorLocator = validatorLocator;
        }

        public async Task<List<UserInvite>> CreateUserInviteListAsync(List<UserInvite> userInviteList, Guid tenantId)
        {
            var userInviteServiceResult = new List<UserInvite>();
            var validUsers = new List<UserInvite>();
            var inValidDomainUsers = new List<UserInvite>();
            var inValidEmailFormatUsers = new List<UserInvite>();

            var tenantDomainsResponse = await _tenantApi.GetTenantDomainsAsync(tenantId);
            if (!tenantDomainsResponse.IsSuccess())
            {
                throw new Exception($"Unable to retrieve tenant domains: {tenantDomainsResponse.ReasonPhrase}");
            }

            var validTenantDomains = tenantDomainsResponse.Payload.Select(d => d.Domain).ToList();
            if (!validTenantDomains.Any())
            {
                throw new Exception("No tenant domains exist");
            }

            var validator = _validatorLocator.GetValidator<EmailValidator>();
            foreach (var newUserInvite in userInviteList)
            {
                if (newUserInvite.Email == null)
                {
                    continue;
                }

                var result = validator.Validate(newUserInvite.Email);
                if (!result.IsValid)
                {
                    newUserInvite.Status = InviteUserStatus.UserEmailFormatInvalid;
                    inValidEmailFormatUsers.Add(newUserInvite);
                    continue;
                }

                var host = new MailAddress(newUserInvite.Email).Host;


                var isUserEmailDomainAllowed = validTenantDomains.Contains(host);

                if (!isUserEmailDomainAllowed)
                {
                    newUserInvite.Status = InviteUserStatus.UserEmailNotDomainAllowed;
                    inValidDomainUsers.Add(newUserInvite);
                }
                else
                {
                    validUsers.Add(newUserInvite);
                }
            }

            if (validUsers.Count > 0)
            {
                validUsers.ForEach(x => x.TenantId = tenantId);
                userInviteServiceResult = await CreateUserInviteInDb(validUsers);
                await SendUserInvites(userInviteServiceResult);
            }

            if (inValidEmailFormatUsers.Count > 0)
            {
                userInviteServiceResult.AddRange(inValidEmailFormatUsers);
            }

            if (inValidDomainUsers.Count > 0)
            {
                userInviteServiceResult.AddRange(inValidDomainUsers);
            }

            return userInviteServiceResult;
        }

        private async Task<List<UserInvite>> SendUserInvites(List<UserInvite> userInviteServiceResult)
        {
            //Filter any duplicate users
            List<UserInvite> validUsers = userInviteServiceResult.FindAll(user => user.Status != InviteUserStatus.DuplicateUserEmail && user.Status != InviteUserStatus.DuplicateUserEntry);
            if (!validUsers.Any())
            {
                return validUsers;
            }
            var emailRequest = _mapper.Map<List<UserInvite>, List<UserEmailRequest>>(validUsers);

            //Mail newly created users
            var userEmailResponses = await _emailApi.SendUserInvite(emailRequest);

            if (userEmailResponses == null || userEmailResponses.Count == 0)
            {
                return validUsers;
            }

            var emailResponse = _mapper.Map<List<UserEmailResponse>, List<UserInvite>>(userEmailResponses);
            await UpdateUserInviteAsync(emailResponse);
            return validUsers;
        }

        public async Task<List<UserInvite>> ResendEmailInviteAsync(List<UserInvite> userInvites, Guid tenantId)
        {
            if (userInvites.Count > 0)
            {

                //User is exist in system or not
                foreach (var userInvite in userInvites)
                {
                    var userInviteDb = await _userInviteRepository.GetItemsAsync(u => u.Email == userInvite.Email);

                    if (userInviteDb.Count() == 0)
                    {
                        userInvite.Status = InviteUserStatus.UserNotExist;
                    }
                    else
                    {
                        userInvite.TenantId = tenantId;
                    }
                }

                var validUsers = userInvites.Where(i => i.Status != InviteUserStatus.UserNotExist).ToList();

                await SendUserInvites(validUsers);

                return userInvites;
            }
            return new List<UserInvite>();
        }

        private async Task<List<UserInvite>> CreateUserInviteInDb(List<UserInvite> userInviteList)
        {
            var invitedEmails = userInviteList.Select(u => u.Email.ToLower());

            var existingSynthesisUsers = await _userRepository.GetItemsAsync(u => invitedEmails.Contains(u.Email.ToLower()));
            var existingUserInvites = await _userInviteRepository.GetItemsAsync(u => invitedEmails.Contains(u.Email.ToLower()));

            var existingSynthesisUsersEmail = existingSynthesisUsers.Select(x => x.Email.ToLower());
            var existingUserInvitesEmail = existingUserInvites.Select(x => x.Email.ToLower());

            var existingEmails = existingSynthesisUsersEmail.Union(existingUserInvitesEmail).ToList();

            var validUsers = userInviteList
                .Where(u => !existingEmails.Contains(u.Email.ToLower()))
                .ToList();

            var duplicateUsers = userInviteList
               .Where(u => existingEmails.Contains(u.Email.ToLower()))
               .ToList();

            duplicateUsers.ForEach(x => x.Status = InviteUserStatus.DuplicateUserEmail);

            var currentUserInvites = new List<UserInvite>();

            if (validUsers.Count > 0)
            {
                foreach (var validUser in validUsers)
                {
                    var isDuplicateUserEntry = currentUserInvites
                        .Exists(u => string.Equals(u.Email, validUser.Email, StringComparison.CurrentCultureIgnoreCase));
                    if (isDuplicateUserEntry)
                    {
                        validUser.Status = InviteUserStatus.DuplicateUserEntry;
                        duplicateUsers.Add(validUser);
                    }
                    else
                    {
                        await _userInviteRepository.CreateItemAsync(validUser);
                        currentUserInvites.Add(validUser);
                    }
                }
            }

            if (duplicateUsers.Count > 0)
            {
                currentUserInvites.AddRange(duplicateUsers);
            }

            return currentUserInvites;
        }

        private async Task UpdateUserInviteAsync(List<UserInvite> userInvite)
        {
            var lastInvitedDates = userInvite.ToDictionary(u => u.Email, u => u.LastInvitedDate);

            foreach (var userInviteupdate in userInvite)
            {
                var userInviteDb = (await _userInviteRepository.GetItemsAsync(u => u.Email == userInviteupdate.Email)).First();
                if (lastInvitedDates[userInviteupdate.Email] != null)
                {
                    userInviteDb.LastInvitedDate = lastInvitedDates[userInviteupdate.Email];
                }
                if (userInviteDb.Id != null)
                {
                    await _userInviteRepository.UpdateItemAsync(userInviteDb.Id.Value, userInviteDb);
                }
            }

        }

        public async Task<PagingMetadata<UserInvite>> GetUsersInvitedForTenantAsync(Guid tenantId, bool allUsers = false)
        {
            return await GetUsersInvitedForTenantFromDb(tenantId, allUsers);
        }

        private async Task<PagingMetadata<UserInvite>> GetUsersInvitedForTenantFromDb(Guid tenantId, bool allUsers)
        {
            var validationResult = await _tenantIdValidator.ValidateAsync(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var existingUserInvites = (await _userInviteRepository.GetItemsAsync(u => u.TenantId == tenantId)).ToList();

            if (!allUsers)
            {
                var invitedEmails = existingUserInvites.Select(i => i.Email).ToList();

                var tenantUsersList = new List<User>();

                var result = await _tenantApi.GetUserIdsByTenantIdAsync(tenantId);
                var userIds = result.Payload.ToList();

                foreach (var batch in invitedEmails.Batch(150))
                {
                    var tenantUsers = await _userRepository.GetItemsAsync(u => userIds.Contains(u.Id.Value) && batch.Contains(u.Email));
                    tenantUsersList.AddRange(tenantUsers);
                }

                var tenantUserEmails = tenantUsersList.Select(s => s.Email);
                existingUserInvites = existingUserInvites.Where(u => !tenantUserEmails.Contains(u.Email)).ToList();
            }

            var returnMetaData = new PagingMetadata<UserInvite>
            {
                List = existingUserInvites
            };
            return returnMetaData;
        }

    }

    public static class ListBatchExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this List<T> items, int maxItems)
        {
            return items.Select((item, inx) => new { item, inx })
                        .GroupBy(x => x.inx / maxItems)
                        .Select(g => g.Select(x => x.item));
        }
    }


}
