using System;
using System.Threading.Tasks;

namespace TASagentTwitchBot.Light
{
    public class LightDemoApplication
    {
        private readonly Core.ICommunication communication;
        private readonly Core.IMessageAccumulator messageAccumulator;
        private readonly Core.ErrorHandler errorHandler;
        private readonly Core.ApplicationManagement applicationManagement;

        public LightDemoApplication(
            Core.ICommunication communication,
            Core.IMessageAccumulator messageAccumulator,
            Core.ErrorHandler errorHandler,
            Core.ApplicationManagement applicationManagement)
        {
            this.communication = communication;
            this.messageAccumulator = messageAccumulator;
            this.errorHandler = errorHandler;
            this.applicationManagement = applicationManagement;
        }

        public async Task RunAsync()
        {
            try
            {
                communication.SendDebugMessage("*** Starting Up ***");
            }
            catch (Exception ex)
            {
                errorHandler.LogFatalException(ex);
            }

            messageAccumulator.MonitorMessages();

            try
            {
                await applicationManagement.WaitForEndAsync();
            }
            catch (Exception ex)
            {
                errorHandler.LogSystemException(ex);
            }

            //Handle Cleanup

            //Oh, there's none left :barbAwk:
        }
    }
}
