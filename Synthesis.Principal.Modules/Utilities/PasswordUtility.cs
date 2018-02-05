using System;
using System.Text;
using SimpleCrypto;

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

        /// <summary>
        ///     Calculates the hash and salt of a password
        /// </summary>
        /// <param name="password">The password to get the hash and salt for</param>
        /// <param name="hash">Param to output the hash of the password</param>
        /// <param name="salt">Param to outptu the salt of the password</param>
        private static void HashAndSalt(string password, out string hash, out string salt)
        {
            //hashing parameters
            const int saltSize = 64;
            const int hashIterations = 10000;

            ICryptoService cryptoService = new SimpleCrypto.PBKDF2();
            hash = cryptoService.Compute(password, saltSize, hashIterations);
            salt = cryptoService.Salt;
        }
    }
}
