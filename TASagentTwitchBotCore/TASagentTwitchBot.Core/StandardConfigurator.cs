using System;
using System.Linq;
using System.Threading.Tasks;

namespace TASagentTwitchBot.Core
{
    public class StandardConfigurator : BaseConfigurator
    {
        public StandardConfigurator(
            Config.BotConfiguration botConfig,
            ICommunication communication,
            ErrorHandler errorHandler)
            : base(botConfig, communication, errorHandler)
        {

        }

        public override Task<bool> VerifyConfigured()
        {
            bool successful = true;

            //Check Accounts
            successful |= ConfigurePassword();

            return Task.FromResult(successful);
        }
    }
}
