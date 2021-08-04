using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TASagentTwitchBot.Core
{
    public interface ICommunication
    {
        public delegate void DebugMessageHandler(string message, MessageType messageType);
        public delegate void ReceiveEventHandler(string message);

        event DebugMessageHandler DebugMessageHandlers;
        event ReceiveEventHandler ReceiveEventHandlers;

        void NotifyEvent(string message);
        void SendDebugMessage(string message);
        void SendWarningMessage(string message);
        void SendErrorMessage(string message);
    }

    public enum MessageType
    {
        Debug = 0,
        Warning,
        Error
    }


    public class CommunicationHandler : ICommunication
    {
        public event ICommunication.DebugMessageHandler DebugMessageHandlers;
        public event ICommunication.ReceiveEventHandler ReceiveEventHandlers;

        public CommunicationHandler() { }

        public void SendDebugMessage(string message)
        {
            DebugMessageHandlers?.Invoke(message, MessageType.Debug);
        }

        public void SendWarningMessage(string message)
        {
            DebugMessageHandlers?.Invoke(message, MessageType.Warning);
        }

        public void SendErrorMessage(string message)
        {
            DebugMessageHandlers?.Invoke(message, MessageType.Error);
        }

        public void NotifyEvent(string message)
        {
            ReceiveEventHandlers?.Invoke(message);
        }
    }
}
