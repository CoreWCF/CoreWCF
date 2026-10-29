// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;

namespace Helpers.Interceptor
{
    /// <summary>
    /// A pass-through binding element that wraps the channel below it and feeds every
    /// outbound and inbound message through an <see cref="IMessageInterceptor"/>. Place
    /// directly above the transport (and below the reliable session) in a CustomBinding.
    /// </summary>
    internal sealed class InterceptingBindingElement : BindingElement
    {
        private readonly IMessageInterceptor _interceptor;

        public InterceptingBindingElement(IMessageInterceptor interceptor)
        {
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
        }

        private InterceptingBindingElement(InterceptingBindingElement other)
        {
            _interceptor = other._interceptor;
        }

        public IMessageInterceptor Interceptor => _interceptor;

        public override BindingElement Clone() => new InterceptingBindingElement(this);

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.GetInnerProperty<T>();
        }

        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return typeof(TChannel) == typeof(IDuplexSessionChannel)
                   && context.CanBuildInnerChannelFactory<TChannel>();
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (typeof(TChannel) != typeof(IDuplexSessionChannel))
            {
                throw new NotSupportedException(
                    $"InterceptingBindingElement only supports IDuplexSessionChannel; requested {typeof(TChannel)}.");
            }

            IChannelFactory<IDuplexSessionChannel> innerFactory =
                context.BuildInnerChannelFactory<IDuplexSessionChannel>();
            object factory = new InterceptingChannelFactory(innerFactory, _interceptor);
            return (IChannelFactory<TChannel>)factory;
        }
    }
}
