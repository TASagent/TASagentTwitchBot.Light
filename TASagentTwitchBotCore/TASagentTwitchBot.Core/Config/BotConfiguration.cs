using System;
using System.Text.Json;
using System.IO;

using TASagentTwitchBot.Core.Web.Middleware;

namespace TASagentTwitchBot.Core.Config
{
    public class BotConfiguration
    {
        private static string ConfigFilePath => BGC.IO.DataManagement.PathForDataFile("Config", "Config.json");
        private static readonly object _lock = new object();

        public string BotName { get; set; } = "";
        public string Broadcaster { get; set; } = "";
        public string BroadcasterId { get; set; } = "";

        public bool LogAllErrors { get; set; } = true;

        public AuthConfiguration AuthConfiguration { get; set; } = new AuthConfiguration();

        public static BotConfiguration GetConfig()
        {
            BotConfiguration config;
            if (File.Exists(ConfigFilePath))
            {
                //Load existing config
                config = JsonSerializer.Deserialize<BotConfiguration>(File.ReadAllText(ConfigFilePath));
            }
            else
            {
                config = new BotConfiguration();
            }

            config.AuthConfiguration.RegenerateAuthStrings();

            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config));

            return config;
        }

        public void Serialize()
        {
            lock (_lock)
            {
                File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(this));
            }
        }
    }

    public class AuthConfiguration
    {
        public bool PublicAuthAllowed { get; set; } = true;

        public CredentialSet Admin { get; set; } = new CredentialSet();

        /// <summary>
        /// Checks the submitted password against the stored passwords to look for a match.
        /// </summary>
        /// <exception cref="FormatException"> Throws <see cref="FormatException"/> if the passwordHash or password is invalid </exception>
        /// <exception cref="Exception"> Throws <see cref="Exception"/> if an exception is encountered in the password validation process </exception>
        public AuthDegree TryCredentials(string password, out string authString)
        {
            if (password is null)
            {
                //At least sanitize against null passwords
                password = "";
            }

            if (Cryptography.ComparePassword(password, Admin.PasswordHash))
            {
                authString = Admin.AuthString;
                return AuthDegree.Admin;
            }

            authString = "";
            return AuthDegree.None;
        }

        public AuthDegree CheckAuthString(string authString)
        {
            if (authString == Admin.AuthString)
            {
                return AuthDegree.Admin;
            }

            return AuthDegree.None;
        }

        public void RegenerateAuthStrings()
        {
            Admin.AuthString = GenerateAuthString();
        }

        private static string GenerateAuthString()
        {
            using var provider = new System.Security.Cryptography.RNGCryptoServiceProvider();
            byte[] bytes = new byte[16];

            provider.GetBytes(bytes);
            return new Guid(bytes).ToString();
        }
    }

    public class CredentialSet
    {
        [Obsolete("The Password field is obsolete. We are hashing it now.")]
        public string Password { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string AuthString { get; set; } = "";
    }
}
