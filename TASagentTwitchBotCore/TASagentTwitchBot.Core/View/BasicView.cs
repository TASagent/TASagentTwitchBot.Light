using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TASagentTwitchBot.Core.View
{
    public class BasicView : IConsoleOutput, IDisposable
    {
        private readonly Config.BotConfiguration botConfig;
        private readonly ICommunication communication;
        private readonly ApplicationManagement applicationManagement;

        private readonly CancellationTokenSource generalTokenSource = new CancellationTokenSource();
        private readonly CountdownEvent readers = new CountdownEvent(1);

        private readonly Channel<ConsoleKeyInfo> consoleChannel;

        private bool disposedValue = false;

        public BasicView(
            Config.BotConfiguration botConfig,
            ICommunication communication,
            ApplicationManagement applicationManagement)
        {
            this.botConfig = botConfig;
            this.communication = communication;
            this.applicationManagement = applicationManagement;

            consoleChannel = Channel.CreateUnbounded<ConsoleKeyInfo>();

            LaunchListeners();

            communication.ReceiveEventHandlers += ReceiveEventHandler;
            communication.DebugMessageHandlers += DebugMessageHandler;

            communication.SendDebugMessage("BasicView Connected.  Listening for Ctrl+Q to quit gracefully.\n");
        }

        private void ReceiveEventHandler(string message)
        {
            Console.WriteLine($"Event   {message}");
        }

        private void DebugMessageHandler(string message, MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Debug:
                    Console.WriteLine(message);
                    break;

                case MessageType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(message);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;

                case MessageType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(message);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;

                default:
                    throw new NotSupportedException($"Unexpected messageType: {messageType}");
            }
        }

        public void LaunchListeners()
        {
            ReadKeysHandler();
            HandleKeysLoop();
        }

        private async Task<ConsoleKeyInfo> WaitForConsoleKeyInfo()
        {
            ConsoleKeyInfo keyInfo = default;
            try
            {
                await Task.Run(() => keyInfo = Console.ReadKey(true));
            }
            catch (Exception ex)
            {
                communication.SendErrorMessage($"BasicView Exception: {ex}");
            }

            return keyInfo;
        }

        private async void ReadKeysHandler()
        {
            try
            {
                readers.AddCount();

                while (true)
                {
                    ConsoleKeyInfo nextKey = await WaitForConsoleKeyInfo().WithCancellation(generalTokenSource.Token);

                    //Bail if we're trying to quit
                    if (generalTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    await consoleChannel.Writer.WriteAsync(nextKey);
                }
            }
            catch (TaskCanceledException) { /* swallow */}
            catch (OperationCanceledException) { /* swallow */}
            catch (Exception ex)
            {
                //Log Error
                communication.SendErrorMessage($"BasicView Exception: {ex}");
            }
            finally
            {
                readers.Signal();
            }
        }

        private async void HandleKeysLoop()
        {
            while (true)
            {
                Console.CursorVisible = false;
                ConsoleKeyInfo input = await consoleChannel.Reader.ReadAsync();

                if (input.Key == ConsoleKey.Q && ((input.Modifiers & ConsoleModifiers.Control) != 0))
                {
                    applicationManagement.TriggerExit();
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    communication.ReceiveEventHandlers -= ReceiveEventHandler;
                    communication.DebugMessageHandlers -= DebugMessageHandler;

                    generalTokenSource.Cancel();

                    readers.Signal();
                    readers.Wait();
                    readers.Dispose();

                    generalTokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
