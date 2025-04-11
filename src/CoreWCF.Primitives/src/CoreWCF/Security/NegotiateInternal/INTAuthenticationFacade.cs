// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal interface INTAuthenticationFacade : IDisposable
    {
        bool IsCompleted { get; }

        string Protocol { get; }

        bool IsValidContext { get; }

        byte[] GetOutgoingBlob(byte[] incomingBlob, bool throwOnError, out object statusCode);

        int Encrypt(byte[] input, ref byte[] output);

        IIdentity GetIdentity();

        Exception CreateExceptionFromError(object securityStatus);
    }
}
