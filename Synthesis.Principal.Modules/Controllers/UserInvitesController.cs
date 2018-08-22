using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using Synthesis.DocumentStorage;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public class UserInvitesController : IUserInvitesController
    {
        private readonly AsyncLazy<IRepository<UserInvite>> _userInviteRepositoryAsyncLazy;
        private readonly AsyncLazy<IRepository<User>> _userRepositoryAsyncLazy;
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
            _userInviteRepositoryAsyncLazy = new AsyncLazy<IRepository<UserInvite>>(() => repositoryFactory.CreateRepositoryAsync<UserInvite>());
            _userRepositoryAsyncLazy = new AsyncLazy<IRepository<User>>(() => repositoryFactory.CreateRepositoryAsync<User>());
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

            var validator = _validatorLocator.GetValidator<BulkUploadEmailValidator>();
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
                userInviteServiceResult = await CreateUserInvitesInDbAsync(validUsers, tenantId);
                await SendUserInvitesAsync(userInviteServiceResult, tenantId);
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

        public async Task<List<UserInvite>> ResendEmailInviteAsync(List<UserInvite> userInvites, Guid tenantId)
        {
            if (!userInvites.Any())
            {
                return new List<UserInvite>();
            }

            var inviteEmails = new HashSet<string>(userInvites.Select(u => u.Email.ToLower()));
            var userInviteRepository = await _userInviteRepositoryAsyncLazy;

            // Get the current invites and resend them.
            var existingInvites = await userInviteRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(tenantId) })
                .Where(u => inviteEmails.Contains(u.Email.ToLower()))
                .ToListAsync();

            foreach (var invite in userInvites)
            {
                if (!existingInvites.Any(e => string.Equals(e.Email, invite.Email, StringComparison.InvariantCultureIgnoreCase)))
                {
                    invite.Status = InviteUserStatus.UserNotExist;
                }
            }

            await SendUserInvitesAsync(existingInvites, tenantId);

            return userInvites;
        }

        public async Task<PagingMetadata<UserInvite>> GetUsersInvitedForTenantAsync(Guid tenantId, bool allUsers = false)
        {
            var validationResult = await _tenantIdValidator.ValidateAsync(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userInviteRepository = await _userInviteRepositoryAsyncLazy;

            var existingUserInvites = await userInviteRepository.CreateItemQuery()
                .Where(u => u.TenantId == tenantId)
                .ToListAsync();

            if (allUsers)
            {
                return new PagingMetadata<UserInvite>
                {
                    List = existingUserInvites
                };
            }

            // Filter out the invited users that are not full users.

            var invitedEmails = existingUserInvites.Select(i => i.Email).ToList();

            var response = await _tenantApi.GetUserIdsByTenantIdAsync(tenantId);
            if (!response.IsSuccess())
            {
                if (response.ResponseCode == HttpStatusCode.NotFound)
                {
                    throw new NotFoundException($"Failed to retrieve user identifiers for tenant {tenantId} because the tenant was not found");
                }

                throw new Exception($"Failed to get user identifiers for tenant {tenantId} due to unexpected result from the tenant service: ResponseCode = {response.ResponseCode}, ReasonPhrase = {response.ReasonPhrase}");
            }

            var userIds = response.Payload.ToList();
            var userRepository = await _userRepositoryAsyncLazy;
            var tenantUsersList = new List<User>();

            // We need to batch the emails otherwise the generated IN clause will make the SQL
            // query string too long.
            foreach (var batch in invitedEmails.Batch(150))
            {
                var tenantUsers = await userRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(tenantId) })
                    .Where(u => userIds.Contains(u.Id.Value) && batch.Contains(u.Email))
                    .ToListAsync();

                tenantUsersList.AddRange(tenantUsers);
            }

            var tenantUserEmails = tenantUsersList.Select(s => s.Email);
            existingUserInvites = existingUserInvites
                .Where(u => !tenantUserEmails.Contains(u.Email))
                .ToList();

            return new PagingMetadata<UserInvite>
            {
                List = existingUserInvites
            };
        }

        private async Task<List<UserInvite>> SendUserInvitesAsync(List<UserInvite> userInvites, Guid tenantId)
        {
            //Filter any duplicate users
            var validUsers = userInvites.FindAll(user => user.Status != InviteUserStatus.DuplicateUserEmail && user.Status != InviteUserStatus.DuplicateUserEntry);
            if (!validUsers.Any())
            {
                return validUsers;
            }

            var userInviteEmailRequests = _mapper.Map<List<UserInvite>, List<UserEmailRequest>>(validUsers);

            //Mail newly created users
            var userEmailResponses = await _emailApi.SendUserInvite(userInviteEmailRequests);

            if (userEmailResponses == null || userEmailResponses.Count == 0)
            {
                return validUsers;
            }

            var sentUserInvites = _mapper.Map<List<UserEmailResponse>, List<UserInvite>>(userEmailResponses);
            await UpdateUserInvitesAsync(sentUserInvites, tenantId);

            return validUsers;
        }

        private async Task<List<UserInvite>> CreateUserInvitesInDbAsync(List<UserInvite> userInviteList, Guid tenantId)
        {
            // The partition key for users is the email domain.
            // We need to group the invite list by email domains so we don't perform a cross-
            // partition query.

            var emailDomainGroupings = userInviteList
                .Select(u => u.Email.ToLower())
                .Select(a => new { Email = a, Domain = a.Substring(a.IndexOf('@')) })
                .GroupBy(p => p.Domain);

            var userRepository = await _userRepositoryAsyncLazy;
            var existingUserEmailAddrs = new List<string>();
            var requestedInviteEmailAddrs = new List<string>();

            // Gather the emails for all existing users that are in the invite list.
            foreach (var grouping in emailDomainGroupings)
            {
                var emails = grouping.Select(p => p.Email).ToList();
                requestedInviteEmailAddrs.AddRange(emails);

                var existingUserEmailsForDomain = await userRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(grouping.Key) })
                    .Where(u => emails.Contains(u.Email.ToLower()))
                    .Select(u => u.Email.ToLower())
                    .ToListAsync();

                existingUserEmailAddrs.AddRange(existingUserEmailsForDomain);
            }

            var userInviteRepository = await _userInviteRepositoryAsyncLazy;
            var existingUserInviteEmails = await userInviteRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(tenantId) })
                .Where(u => requestedInviteEmailAddrs.Contains(u.Email.ToLower()))
                .Select(u => u.Email.ToLower())
                .ToListAsync();

            var existingEmailAddrs = existingUserEmailAddrs.Union(existingUserInviteEmails).ToList();

            var validUserInvites = userInviteList
                .Where(u => !existingEmailAddrs.Contains(u.Email.ToLower()))
                .ToList();

            var duplicateUsers = userInviteList
               .Where(u => existingEmailAddrs.Contains(u.Email.ToLower()))
               .Select(u =>
               {
                   u.Status = InviteUserStatus.DuplicateUserEmail;
                   return u;
               })
               .ToList();

            var currentUserInvites = new List<UserInvite>();

            if (validUserInvites.Count > 0)
            {
                foreach (var validUser in validUserInvites)
                {
                    var isDuplicateUserEntry = currentUserInvites
                        .Exists(u => string.Equals(u.Email, validUser.Email, StringComparison.InvariantCultureIgnoreCase));
                    if (isDuplicateUserEntry)
                    {
                        validUser.Status = InviteUserStatus.DuplicateUserEntry;
                        duplicateUsers.Add(validUser);
                    }
                    else
                    {
                        await userInviteRepository.CreateItemAsync(validUser);
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

        private async Task UpdateUserInvitesAsync(List<UserInvite> userInvites, Guid tenantId)
        {
            var lastInvitedDates = userInvites.ToDictionary(u => u.Email, u => u.LastInvitedDate);
            var emails = new HashSet<string>(userInvites.Select(i => i.Email));

            var userInviteRepository = await _userInviteRepositoryAsyncLazy;
            var existingUserInvites = await userInviteRepository.CreateItemQuery(new BatchOptions { PartitionKey = new PartitionKey(tenantId) })
                .Where(u => emails.Contains(u.Email))
                .ToListAsync();

            foreach (var userInvite in existingUserInvites)
            {
                if (lastInvitedDates.TryGetValue(userInvite.Email, out var lastInvitedDate))
                {
                    userInvite.LastInvitedDate = lastInvitedDate;
                }

                if (userInvite.Id != null)
                {
                    await userInviteRepository.UpdateItemAsync(userInvite.Id.Value, userInvite);
                }
            }
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