// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    internal static class MessageExtensions
    {
        internal static Exception CreateMessageDisposedException(this Message _)
        {
            return new ObjectDisposedException("", SR.MessageClosed);
        }
    }
}
