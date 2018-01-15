using System;
using System.Text;

namespace Synthesis.PrincipalService.Utilities
{
    public class PasswordUtility : IPasswordUtility
    {
        /// <summary>
        ///     Method for generating a new random password
        /// </summary>
        /// <param name="length">Desired length of the password to be returned</param>
        public string GenerateRandomPassword(int length)
        {
            const string valid = @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890~!@#$%^&*()_+-={}|:<>?[]\;',./'";
            var res = new StringBuilder();
            var rnd = new Random();
            while (0 < length--)
            {
                res.Append(valid[rnd.Next(valid.Length)]);
            }

            return res.ToString();
        }
    }
}
