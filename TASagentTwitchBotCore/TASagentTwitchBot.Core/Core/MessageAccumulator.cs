using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.SignalR;

using TASagentTwitchBot.Core.Web.Hubs;

namespace TASagentTwitchBot.Core
{
    public interface IMessageAccumulator
    {
        void MonitorMessages();
        MessageBlock<SimpleMessage> GetAllEvents();
        MessageBlock<SimpleMessage> GetAllDebugs();

        bool IsAuthenticatedUser(string connectionId);
        void AddAuthenticatedUser(string connectionId);
        void ClearAuthenticatedUsers();
    }

    public class MessageAccumulator : IMessageAccumulator, IDisposable
    {
        private readonly IHubContext<MonitorHub> monitorHubContext;

        private readonly HashSet<string> authenticatedUsers = new HashSet<string>();

        private readonly MessageBuffer<SimpleMessage> eventBuffer = new MessageBuffer<SimpleMessage>(1000);
        private readonly MessageBuffer<SimpleMessage> debugBuffer = new MessageBuffer<SimpleMessage>(1000);

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CountdownEvent readers = new CountdownEvent(1);
        private bool disposedValue;

        public MessageAccumulator(
            ICommunication communication,
            IHubContext<MonitorHub> monitorHubContext)
        {
            this.monitorHubContext = monitorHubContext;

            communication.ReceiveEventHandlers += ReceiveEvent;
            communication.DebugMessageHandlers += ReceiveDebugMessage;
        }

        public async void MonitorMessages()
        {
            try
            {
                readers.AddCount();

                while (true)
                {
                    if (eventBuffer.PendingMessages > 0)
                    {
                        //Handle
                        await monitorHubContext.Clients.Group("Authenticated").SendAsync(
                            "ReceiveNewEvents", eventBuffer.GetPendingMessages());
                    }

                    if (debugBuffer.PendingMessages > 0)
                    {
                        //Handle
                        await monitorHubContext.Clients.Group("Authenticated").SendAsync(
                            "ReceiveNewDebugs", debugBuffer.GetPendingMessages());
                    }

                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException) { /* swallow */}
            catch (ThreadAbortException) { /* swallow */}
            catch (ObjectDisposedException) { /* swallow */}
            finally
            {
                readers.Signal();
            }
        }

        public MessageBlock<SimpleMessage> GetAllEvents() => eventBuffer.GetAllMessages();
        public MessageBlock<SimpleMessage> GetAllDebugs() => debugBuffer.GetAllMessages();

        private void ReceiveDebugMessage(string message, MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Debug:
                    debugBuffer.AddMessage(new SimpleMessage(HttpUtility.HtmlEncode(message)));
                    break;

                case MessageType.Warning:
                    debugBuffer.AddMessage(new SimpleMessage($"<span style=\"color: #FFFF00\">{HttpUtility.HtmlEncode(message)}</span>"));
                    break;

                case MessageType.Error:
                    debugBuffer.AddMessage(new SimpleMessage($"<span style=\"color: #FF0000\">{HttpUtility.HtmlEncode(message)}</span>"));
                    break;

                default:
                    throw new Exception($"Unexpected MessageType: {message}");
            }
        }

        private void ReceiveEvent(string message)
        {
            eventBuffer.AddMessage(new SimpleMessage(message));
        }

        public bool IsAuthenticatedUser(string connectionId) => authenticatedUsers.Contains(connectionId);

        public void AddAuthenticatedUser(string connectionId) => authenticatedUsers.Add(connectionId);

        public void ClearAuthenticatedUsers()
        {
            foreach (string user in authenticatedUsers)
            {
                monitorHubContext.Groups.RemoveFromGroupAsync(user, "Authenticated");
            }

            authenticatedUsers.Clear();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Cancel();

                    readers.Signal();
                    readers.Wait();
                    readers.Dispose();

                    cancellationTokenSource.Dispose();
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

    public class MessageBuffer<T>
    {
        private readonly Dictionary<int, T> messageDict = new Dictionary<int, T>();

        private readonly int capacity;

        private int oldestIndex = 0;
        private int currentIndex = 0;
        private int lastOutputIndex = 0;

        public int PendingMessages => currentIndex - lastOutputIndex;

        public MessageBuffer(int capacity)
        {
            this.capacity = capacity;
        }

        public bool RemoveFirst(Func<T, bool> selector)
        {
            if (selector == null)
            {
                return false;
            }

            int key = -1;
            foreach (var pair in messageDict)
            {
                if (selector.Invoke(pair.Value))
                {
                    key = pair.Key;
                    break;
                }
            }

            if (key == -1)
            {
                return false;
            }

            messageDict.Remove(key);
            return true;
        }

        public void Clear()
        {
            messageDict.Clear();
            oldestIndex = 0;
            currentIndex = 0;
            lastOutputIndex = 0;
        }

        public void AddMessage(T newMessage)
        {
            messageDict.Add(currentIndex++, newMessage);

            while (messageDict.Count > capacity)
            {
                messageDict.Remove(oldestIndex++);
            }
        }

        public MessageBlock<T> GetAllMessages()
        {
            return new MessageBlock<T>(messageDict.Values.ToList());
        }

        public MessageBlock<T> GetPendingMessages()
        {
            List<T> newMessages = new List<T>(PendingMessages);

            for (int i = lastOutputIndex; i < currentIndex; i++)
            {
                if (messageDict.ContainsKey(i))
                {
                    newMessages.Add(messageDict[i]);
                }
            }

            lastOutputIndex = currentIndex;

            return new MessageBlock<T>(newMessages);
        }

    }

    public record MessageBlock<T>(List<T> Messages);
    public record SimpleMessage(string Message);
    public record NotificationMessage(int Id, string Message);
}
