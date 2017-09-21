﻿using AutoMapper;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Dao.Models;
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
        private readonly ILogger _logger;
        private readonly IEmailUtility _emailUtility;
        private readonly IMapper _mapper;

        public UserInvitesController(
            IRepositoryFactory repositoryFactory,
            ILogger logger,
            IEmailUtility emailUtility,
            IMapper mapper)
        {
            _userInviteRepository = repositoryFactory.CreateRepository<UserInvite>();
            _userRepository = repositoryFactory.CreateRepository<User>();
            _logger = logger;
            _emailUtility = emailUtility;
            _mapper = mapper;
        }

        public async Task<List<UserInviteResponse>> CreateUserInviteListAsync(List<UserInviteRequest> userInviteList, Guid tenantId)
        {
            var userInviteServiceResult =new List<UserInviteResponse>();
            var validUsers = new List<UserInviteResponse>();
            var inValidDomainUsers = new List<UserInviteResponse>();
            var inValidEmailFormatUsers = new List<UserInviteResponse>();
            //TODO Get Valid and invalid Tenant domain
            //var validTenantDomains = _collaborationService.GetTenantById(tenantId).Payload.TenantDomains;
            //var inValidFreeDomains = await _collaborationService.GetFreeEmailDomainsAsync();

            List<String> validTenantDomains = new List<string> { "dispostable.com", "yopmail.com" };
            List<String> inValidFreeDomains = new List<string> { "aol.com", "gmail.com", "hotmail.com" } ;
            
            var userInviteEntityList = _mapper.Map<List<UserInviteRequest>, List<UserInviteResponse>>(userInviteList);

            foreach (var newUserInvite in userInviteEntityList)
            {
                if (!EmailValidator.IsValidForBulkUpload(newUserInvite.Email))
                {
                    newUserInvite.Status = InviteUserStatus.UserEmailFormatInvalid;
                    inValidEmailFormatUsers.Add(newUserInvite);
                    continue;
                }

                var host = new MailAddress(newUserInvite.Email).Host;

                var isFreeEmailDomain = inValidFreeDomains.Contains(host);

                var isUserEmailDomainAllowed = validTenantDomains.Contains(host);

                if (isFreeEmailDomain)
                {
                    newUserInvite.Status = InviteUserStatus.UserEmailDomainFree;
                    inValidDomainUsers.Add(newUserInvite);
                }
                else if (!isUserEmailDomainAllowed)
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

                //Filter any duplicate users
                validUsers = userInviteServiceResult.FindAll(user => user.Status != InviteUserStatus.DuplicateUserEmail && user.Status != InviteUserStatus.DuplicateUserEntry);

                //Mail newly created users
                var usersMailed = _emailUtility.SendUserInvite(validUsers);

                
                if (usersMailed)
                    await UpdateUserInviteAsync(validUsers);
            }

            if (inValidEmailFormatUsers.Count > 0)
                userInviteServiceResult.AddRange(inValidEmailFormatUsers);

            if (inValidDomainUsers.Count > 0)
                userInviteServiceResult.AddRange(inValidDomainUsers);

            return userInviteServiceResult;
        }

        public async Task<List<UserInviteResponse>> ResendEmailInviteAsync(List<UserInviteRequest> userInviteList, Guid tenantId)
        {
            if (userInviteList.Count > 0)
            {
                var userInvites = _mapper.Map<List<UserInviteRequest>, List<UserInviteResponse>>(userInviteList);

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

                var userReinvited = _emailUtility.SendUserInvite(validUsers);

                if (userReinvited)
                    await UpdateUserInviteAsync(validUsers);
                
                return userInvites;
            }
            return new List<UserInviteResponse>();
        }

        private async Task<List<UserInviteResponse>> CreateUserInviteInDb(List<UserInviteResponse> userInviteList)
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

            var currentUserInvites = new List<UserInviteResponse>();

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
                        await _userInviteRepository.CreateItemAsync(_mapper.Map<UserInviteResponse, UserInvite>(validUser));
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

        private async Task UpdateUserInviteAsync(List<UserInviteResponse> userInvite)
        {
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