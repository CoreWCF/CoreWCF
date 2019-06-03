using System;
using CoreWCF.Channels;

namespace CoreWCF
{
    public sealed class UnknownMessageReceivedEventArgs : EventArgs
    {
        Message message;

        internal UnknownMessageReceivedEventArgs(Message message)
        {
            this.message = message;
        }

        public Message Message
        {
            get { return message; }
        }
    }
}