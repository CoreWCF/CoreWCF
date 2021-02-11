// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace CoreWCF.Security.NegotiateInternal
{
    internal interface INegotiateInternalState : IDisposable
    {
        string GetOutgoingBlob(string incomingBlob, out BlobErrorType status, out Exception error);

        bool IsCompleted { get; }

        string Protocol { get; }

        IIdentity GetIdentity();
    }

    internal enum BlobErrorType
    {
        None,
        CredentialError,
        ClientError,
        Other
    }
}
