﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Security
{
    internal interface ISspiNegotiation : IDisposable
    {
        //DateTime ExpirationTimeUtc { get; }

        /// <summary>
        /// This indicates if the handshake is complete or not. 
        /// Note that the IsValidContext flag indicates if the handshake ended in
        /// success or failure
        /// </summary>
        bool IsCompleted { get; }

        bool IsValidContext { get; }

        string KeyEncryptionAlgorithm { get; }

        byte[] Decrypt(byte[] encryptedData);

        byte[] Encrypt(byte[] data);

        byte[] GetOutgoingBlob(
          byte[] incomingBlob,
          ChannelBinding channelbinding,
          ExtendedProtectionPolicy protectionPolicy);

        string GetRemoteIdentityName();
    }

    internal interface ISspiNegotiationInfo
    {
        ISspiNegotiation SspiNegotiation { get; }
    }
}
