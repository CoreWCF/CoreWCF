// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    public abstract class ChannelManagerBase : CommunicationObject, IDefaultCommunicationTimeouts
    {
        protected ChannelManagerBase() { }

        protected abstract TimeSpan DefaultReceiveTimeout { get; }
        protected abstract TimeSpan DefaultSendTimeout { get; }
        internal TimeSpan InternalReceiveTimeout => DefaultReceiveTimeout;
        internal TimeSpan InternalSendTimeout => DefaultSendTimeout;
        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout => DefaultCloseTimeout;
        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout => DefaultOpenTimeout;
        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout => DefaultReceiveTimeout;
        TimeSpan IDefaultCommunicationTimeouts.SendTimeout => DefaultSendTimeout;

        internal Exception CreateChannelTypeNotSupportedException(Type type)
        {
            return new ArgumentException(SR.Format(SR.ChannelTypeNotSupported, type), "TChannel");
        }
    }
}
