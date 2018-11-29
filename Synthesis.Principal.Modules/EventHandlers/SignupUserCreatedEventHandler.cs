using System.Linq;
using System.Threading.Tasks;
using Synthesis.EventBus;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.TrialService.InternalApi.Models;

namespace Synthesis.PrincipalService.EventHandlers
{
    public class SignupUserCreatedEventHandler : IAsyncEventHandler<TrialSignupUser>
    {
        private readonly IGroupsController _groupsController;
        private readonly IUsersController _userController;

        public SignupUserCreatedEventHandler(IGroupsController groupsController, IUsersController userController)
        {
            _groupsController = groupsController;
            _userController = userController;
        }

        /// <inheritdoc />
        public async Task HandleEventAsync(TrialSignupUser payload)
        {
            var groups = await _groupsController.GetGroupsForTenantAsync(payload.TenantId);
            var adminGroup = groups.FirstOrDefault(item => item.Type == GroupType.TenantAdmin);

            if (adminGroup != null)
            {
                var userGroup = new UserGroup();
                userGroup.GroupId = adminGroup.Id.Value;
                userGroup.UserId = payload.UserId;
                await _userController.CreateUserGroupAsync(userGroup, payload.UserId);
            }
        }
    }
}
