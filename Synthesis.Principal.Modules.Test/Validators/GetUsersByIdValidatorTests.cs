using System;
using System.Collections.Generic;
using System.Linq;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class GetUsersByIdValidatorTests
    {
        [Fact]
        public void ShouldFailOnEmptyUserIdList()
        {
            var userIds = new List<Guid>();
            var validator = new GetUsersByIdValidator();

            var result = validator.Validate(userIds);
            Assert.False(result.IsValid);
        }

        public static IEnumerable<object[]> GetIds()
        {
            yield return new object[] { new List<Guid> { Guid.Empty } };
            yield return new object[] { new List<Guid> { Guid.Empty, Guid.Empty } };
            yield return new object[] { new List<Guid> { Guid.NewGuid(), Guid.Empty, Guid.Empty} };
            yield return new object[] { new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.Empty } };
        }

        [Theory]
        [MemberData(nameof(GetIds))]
        public void ShouldFailOnEmptyUserIdInList(IEnumerable<Guid> userIds)
        {
            var validator = new GetUsersByIdValidator();

            var result = validator.Validate(userIds.ToList());
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidListOfUserIds()
        {
            var userIds = new List<Guid> { Guid.NewGuid() };
            var validator = new GetUsersByIdValidator();

            var result = validator.Validate(userIds);
            Assert.True(result.IsValid);
        }
    }
}
