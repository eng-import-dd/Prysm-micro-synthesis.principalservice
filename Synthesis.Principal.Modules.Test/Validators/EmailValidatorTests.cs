using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class EmailValidatorTests
    {
        /// <summary>
        /// All test cases taken from https://gist.github.com/cjaoude/fd9910626629b53c4d25.
        /// </summary>
        /// <param name="emailAddressToTest"></param>
        /// <param name="expected"></param>
        [Theory]
        [InlineData("email@example.com", true)]
        [InlineData("firstname.lastname@example.com", true)]
        [InlineData("email@subdomain.example.com", true)]
        [InlineData("firstname+lastname@example.com", true)]
        [InlineData("email@123.123.123.123", true)]
        [InlineData("email@[123.123.123.123]", true)]
        [InlineData("\"email\"@example.com", true)]
        [InlineData("1234567890@example.com", true)]
        [InlineData("email@example-one.com", true)]
        [InlineData("_______@example.com", true)]
        [InlineData("email@example.name", true)]
        [InlineData("email@example.museum", true)]
        [InlineData("email@example.co.jp", true)]
        [InlineData("firstname-lastname@example.com", true)]
        [InlineData("much.”more\\ unusual”@example.com", true)]
        [InlineData("very.unusual.”@”.unusual.com@example.com", true)]
        [InlineData("very.”(),:;<>[]”.VERY.”very@\\ \"very\\”.unusual@strange.example.com", true)]
        [InlineData("plainaddress", false)]
        [InlineData("#@%^%#$@#$@#.com", false)]
        [InlineData("@example.com", false)]
        [InlineData("Joe Smith <email@example.com>", false)]
        [InlineData("email.example.com", false)]
        [InlineData("email@example@example.com", false)]
        [InlineData(".email@example.com", false)]
        [InlineData("email.@example.com", false)]
        [InlineData("email..email@example.com", false)]
        [InlineData("あいうえお@example.com", false)]
        [InlineData("email@example.com (Joe Smith)", false)]
        [InlineData("email@example", false)]
        [InlineData("email@-example.com", false)]
        [InlineData("email@example.web", false)]
        [InlineData("email@111.222.333.44444", false)]
        [InlineData("email@example..com", false)]
        [InlineData("Abc..123@example.com", false)]
        [InlineData("”(),:;<>[\\]@example.com", false)]
        [InlineData("just”not”right@example.com", false)]
        [InlineData("this\\ is\"really\"not\\allowed@example.com", false)]
        public void ValidationTests(string emailAddressToTest, bool expected)
        {
            var validator = new EmailValidator("Email");
            var result = validator.Validate(emailAddressToTest);
            if (expected)
            {
                Assert.True(expected);
            }
            else
            {
                Assert.False(expected);
            }
        }
    }
}
