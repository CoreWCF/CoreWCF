// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;

namespace CoreWCF.Channels
{
    internal abstract class ServiceChannelBase : CommunicationObject, IChannel, IDefaultCommunicationTimeouts
    {
        private readonly IDefaultCommunicationTimeouts _timeouts;

        protected ServiceChannelBase(IDefaultCommunicationTimeouts timeouts)
        {
            _timeouts = new ImmutableCommunicationTimeouts(timeouts);
        }

        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout => DefaultCloseTimeout;

        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout => DefaultOpenTimeout;

        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout => DefaultReceiveTimeout;

        TimeSpan IDefaultCommunicationTimeouts.SendTimeout => DefaultSendTimeout;

        protected override TimeSpan DefaultCloseTimeout => _timeouts.CloseTimeout;

        protected override TimeSpan DefaultOpenTimeout => _timeouts.OpenTimeout;

        protected TimeSpan DefaultReceiveTimeout => _timeouts.ReceiveTimeout;

        protected TimeSpan DefaultSendTimeout => _timeouts.SendTimeout;

        public virtual IServiceChannelDispatcher ChannelDispatcher { get; set; }

        public virtual T GetProperty<T>() where T : class
        {
            return null;
        }
    }
}
