// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal interface INegotiateInternalState : IDisposable
    {
        void SetChannelBinding(ChannelBinding channelBinding);

        void SetExtendedProtectionPolicy(ExtendedProtectionPolicy protectionPolicy);

        byte[] GetOutgoingBlob(byte[] incomingBlob, out BlobErrorType status, out Exception error);

        bool IsCompleted { get; }

        bool IsValidContext { get; }

        string Protocol { get; }

        IIdentity GetIdentity();

        byte[] Encrypt(byte[] input);
    }

    internal enum BlobErrorType
    {
        None,
        CredentialError,
        ClientError,
        Other
    }
}
