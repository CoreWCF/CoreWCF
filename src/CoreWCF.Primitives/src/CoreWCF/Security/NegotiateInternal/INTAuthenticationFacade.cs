// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal interface INTAuthenticationFacade : IDisposable
    {
        void SetChannelBinding(ChannelBinding channelBinding);

        void SetExtendedProtectionPolicy(ExtendedProtectionPolicy protectionPolicy);

        bool IsCompleted { get; }

        string Protocol { get; }

        bool IsValidContext { get; }

        byte[] GetOutgoingBlob(byte[] incomingBlob, out NegotiateInternalSecurityStatusPal status);

        byte[] Encrypt(byte[] input);

        IIdentity GetIdentity();
    }
}
