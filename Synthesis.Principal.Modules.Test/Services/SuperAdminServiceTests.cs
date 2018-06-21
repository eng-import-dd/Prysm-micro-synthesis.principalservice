using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Services;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Services
{
    public class SuperAdminServiceTests
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly SuperAdminService _target;

        public SuperAdminServiceTests()
        {
            _repositoryFactoryMock
                .Setup(m => m.CreateRepository<User>())
                .Returns(_userRepositoryMock.Object);

            _target = new SuperAdminService(_repositoryFactoryMock.Object);
        }

        [Fact]
        public async Task IsSuperAdminAsyncReturnsTrueIfUserIsInSuperAdminGroup()
        {
            _userRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User {Groups = new List<Guid>()
                {
                    GroupIds.SuperAdminGroupId
                }});

            var result = await _target.IsSuperAdminAsync(Guid.NewGuid());

            Assert.True(result);
        }

        [Fact]
        public async Task IsSuperAdminAsyncReturnsFalseIfUserIsNotFound()
        {
            _userRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            var result = await _target.IsSuperAdminAsync(Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task UserIsLastSuperAdminReturnsFalseIfCurrentUserIsNotASuperAdmin()
        {
            _userRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            var result = await _target.UserIsLastSuperAdminAsync(Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task UserIsLastSuperAdminReturnsTrueIfThereAreNoOtherSuperAdmins()
        {
            _userRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User
                {
                    Groups = new List<Guid>()
                    {
                        GroupIds.SuperAdminGroupId
                    }
                });

            _userRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            var result = await _target.UserIsLastSuperAdminAsync(Guid.NewGuid());

            Assert.True(result);
        }

        [Fact]
        public async Task UserIsLastSuperAdminReturnsFalseIfThereIsAnotherSuperAdmin()
        {
            _userRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User
                {
                    Groups = new List<Guid>()
                    {
                        GroupIds.SuperAdminGroupId
                    }
                });

            _userRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var result = await _target.UserIsLastSuperAdminAsync(Guid.NewGuid());

            Assert.False(result);
        }
    }
}
