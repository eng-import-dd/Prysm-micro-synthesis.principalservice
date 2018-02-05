﻿using System;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class CreateUserGroupRequestValidatorTests
    {
        private readonly CreateUserGroupRequestValidator _validator = new CreateUserGroupRequestValidator();

        [Fact]
        public void ShouldFailIfUserIdIsEmpty()
        {
            var request = CreateUserGroupRequest.Example();
            request.UserId = Guid.Empty;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfGroupIdIsEmpty()
        {
            var request = CreateUserGroupRequest.Example();
            request.GroupId = Guid.Empty;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfRequestObjectEmpty()
        {
            var request = new CreateUserGroupRequest();
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValidRequestObject()
        {
            var request = CreateUserGroupRequest.Example();

            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
