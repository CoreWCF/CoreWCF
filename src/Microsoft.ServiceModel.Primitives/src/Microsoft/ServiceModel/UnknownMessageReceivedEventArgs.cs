using System;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
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