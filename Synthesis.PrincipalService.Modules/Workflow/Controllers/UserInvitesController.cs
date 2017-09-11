using AutoMapper;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public class UserInvitesController : IUserInvitesController
    {
        private readonly IRepository<UserInvite> _userInviteRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly IEmailUtility _emailUtility;
        private readonly IMapper _mapper;

        public UserInvitesController(
            IRepositoryFactory repositoryFactory,
            IEventService eventService,
            ILogger logger,
            IEmailUtility emailUtility,
            IMapper mapper)
        {
            _userInviteRepository = repositoryFactory.CreateRepository<UserInvite>();
            _userRepository = repositoryFactory.CreateRepository<User>();
            _eventService = eventService;
            _logger = logger;
            _emailUtility = emailUtility;
            _mapper = mapper;
        }

        public async Task<List<UserInviteResponse>> CreateUserInviteListAsync(List<UserInviteRequest> userInviteList, Guid tenantId)
        {
            var userInviteServiceResult =new List<UserInviteEntity>();
            var validUsers = new List<UserInviteEntity>();
            var inValidDomainUsers = new List<UserInviteEntity>();
            var inValidEmailFormatUsers = new List<UserInviteEntity>();
            //Get Valid and invalid account domain
            //var validAccountDomains = _collaborationService.GetAccountById(accountId).Payload.AccountDomains;
            //var inValidFreeDomains = await _collaborationService.GetFreeEmailDomainsAsync();

            string[] validAccountDomains = { "dispostable.com", "yopmail.com" };
            string[] inValidFreeDomains = { "aol.com", "gmail.com", "hotmail.com" } ;
            
            var userInviteEntityList = _mapper.Map<List<UserInviteRequest>, List<UserInviteEntity>>(userInviteList);

            foreach (var newUserInvite in userInviteEntityList)
            {
                if (!EmailValidator.IsValidForBulkUpload(newUserInvite.Email))
                {
                    newUserInvite.IsUserEmailFormatInvalid = true;
                    inValidEmailFormatUsers.Add(newUserInvite);
                    continue;
                }

                var host = new MailAddress(newUserInvite.Email).Host;

                var isFreeEmailDomain = inValidFreeDomains.Contains(host);

                var isUserEmailDomainAllowed = validAccountDomains.Contains(host);

                if (isFreeEmailDomain)
                {
                    newUserInvite.IsUserEmailDomainFree = true;
                    inValidDomainUsers.Add(newUserInvite);
                }
                else if (!isUserEmailDomainAllowed)
                {
                    newUserInvite.IsUserEmailDomainAllowed = false;
                    inValidDomainUsers.Add(newUserInvite);
                }
                else
                {
                    newUserInvite.IsUserEmailDomainFree = false;
                    newUserInvite.IsUserEmailDomainAllowed = true;
                    validUsers.Add(newUserInvite);
                }
            }

            if (validUsers.Count > 0)
            {
                validUsers.ForEach(x => x.TenantId = tenantId);
                userInviteServiceResult = await CreateUserInviteInDb(validUsers);

                //Filter any duplicate users
                validUsers = userInviteServiceResult.FindAll(user => user.IsDuplicateUserEmail == false && user.IsDuplicateUserEntry == false);

                //Mail newly created users
                var usersMailed = _emailUtility.SendUserInvite(validUsers);

                
                if (usersMailed)
                    await UpdateUserInviteAsync(validUsers);
            }

            if (inValidEmailFormatUsers.Count > 0)
                userInviteServiceResult.AddRange(inValidEmailFormatUsers);

            if (inValidDomainUsers.Count > 0)
                userInviteServiceResult.AddRange(inValidDomainUsers);

            return _mapper.Map<List<UserInviteEntity>, List<UserInviteResponse>>(userInviteServiceResult);
        }

        private async Task<List<UserInviteEntity>> CreateUserInviteInDb(List<UserInviteEntity> userInviteList)
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

            duplicateUsers.ForEach(x => x.IsDuplicateUserEmail = true);

            var currentUserInvites = new List<UserInviteEntity>();

            if (validUsers.Count > 0)
            {
                foreach (var validUser in validUsers)
                {
                    var isDuplicateUserEntry = currentUserInvites
                        .Exists(u => string.Equals(u.Email, validUser.Email, StringComparison.CurrentCultureIgnoreCase));
                    if (isDuplicateUserEntry)
                    {
                        validUser.IsDuplicateUserEntry = true;
                        duplicateUsers.Add(validUser);
                    }
                    else
                    {
                        await _userInviteRepository.CreateItemAsync(_mapper.Map<UserInviteEntity, UserInvite>(validUser));
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

        private async Task UpdateUserInviteAsync(List<UserInviteEntity> userInvite)
        {
            var invitedEmails = userInvite.Select(u => u.Email).ToList();
            var lastInvitedDates = userInvite.ToDictionary(u => u.Email, u => u.LastInvitedDate);

            foreach (var userInviteupdate in userInvite)
            {
                var userInviteDb = (await _userInviteRepository.GetItemsAsync(u => u.Email == userInviteupdate.Email)).First();
                if (lastInvitedDates[userInviteupdate.Email] != null)
                {
                    userInviteDb.LastInvitedDate = lastInvitedDates[userInviteupdate.Email].Value;
                }
                if (userInviteDb.Id != null)
                {
                    await _userInviteRepository.UpdateItemAsync(userInviteDb.Id.Value, userInviteDb);
                }
            }

        }
    }
}
