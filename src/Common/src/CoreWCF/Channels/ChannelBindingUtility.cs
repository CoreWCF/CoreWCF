// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Security;
using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Channels
{
    internal static class ChannelBindingUtility
    {
        private static readonly ExtendedProtectionPolicy s_disabledPolicy = new ExtendedProtectionPolicy(PolicyEnforcement.Never);

        public static ExtendedProtectionPolicy DefaultPolicy { get; } = s_disabledPolicy;

        public static ChannelBinding GetToken(SslStream stream)
        {
            return GetToken(stream.TransportContext);
        }

        public static ChannelBinding GetToken(TransportContext context)
        {
            ChannelBinding token = null;
            if (context != null)
            {
                token = context.GetChannelBinding(ChannelBindingKind.Endpoint);
            }
            return token;
        }

        public static void TryAddToMessage(ChannelBinding channelBindingToken, Message message, bool messagePropertyOwnsCleanup)
        {
            if (channelBindingToken != null)
            {
                ChannelBindingMessageProperty property = new ChannelBindingMessageProperty(channelBindingToken, messagePropertyOwnsCleanup);
                property.AddTo(message);
                ((IDisposable)property).Dispose(); //message.Properties.Add() creates a copy...
            }
        }

        public static void Dispose(ref ChannelBinding channelBinding)
        {
            // Explicitly cast to IDisposable to avoid the SecurityException.
            // I don't think this is relevant on .Net Core but I don't believe
            // it will emit any extra code from the JIT.
            IDisposable disposable = (IDisposable)channelBinding;
            channelBinding = null;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}
