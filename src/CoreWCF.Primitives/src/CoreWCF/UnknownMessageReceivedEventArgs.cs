// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF
{
    public sealed class UnknownMessageReceivedEventArgs : EventArgs
    {
        private Message message;

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