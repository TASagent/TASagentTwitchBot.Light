using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TASagentTwitchBot.Core
{
    public abstract class BaseConfigurator : IConfigurator
    {
        protected readonly Config.BotConfiguration botConfig;

        public BaseConfigurator(
            Config.BotConfiguration botConfig,
            ICommunication communication,
            ErrorHandler errorHandler)
        {
            this.botConfig = botConfig;

            //Assign library log handlers
            BGC.Debug.ExceptionCallback += errorHandler.LogExternalException;

            BGC.Debug.LogCallback += communication.SendDebugMessage;
            BGC.Debug.LogWarningCallback += communication.SendWarningMessage;
            BGC.Debug.LogErrorCallback += communication.SendErrorMessage;
        }

        public abstract Task<bool> VerifyConfigured();

        protected void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nERROR:   {message}\n");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        protected void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nWARNING: {message}\n");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        protected void WritePrompt(string message)
        {
            Console.Write($"Enter {message}:\n    > ");
        }

        protected void WriteMessage(string message)
        {
            Console.WriteLine(message);
        }

        protected bool ConfigurePassword()
        {
            bool successful = true;
            if (string.IsNullOrEmpty(botConfig.AuthConfiguration.Admin.PasswordHash))
            {
                WritePrompt("Admin password for bot control");

                string pass = Console.ReadLine()?.Trim();

                if (!string.IsNullOrEmpty(pass))
                {
                    botConfig.AuthConfiguration.Admin.PasswordHash = Cryptography.HashPassword(pass);
                    botConfig.Serialize();
                }
                else
                {
                    WriteError("Empty Admin Password received.");
                    successful = false;
                }
            }

            return successful;
        }
    }
}
