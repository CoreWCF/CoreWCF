// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Security.Cryptography;
using System;

namespace CoreWCF.Security
{
    internal class SspiNegotiationTokenAuthenticatorState : NegotiationTokenAuthenticatorState
    {
        public SspiNegotiationTokenAuthenticatorState(ISspiNegotiation sspiNegotiation)
            : base()
        {
            SspiNegotiation = sspiNegotiation ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(sspiNegotiation));
            NegotiationDigest = CryptoHelper.NewSha1HashAlgorithm();
        }

        public ISspiNegotiation SspiNegotiation { get; }

        internal int RequestedKeySize { get; set; }

        internal HashAlgorithm NegotiationDigest { get; }

        internal string Context { get; set; }

        internal EndpointAddress AppliesTo { get; set; }

        internal DataContractSerializer AppliesToSerializer { get; set; }

        public override string GetRemoteIdentityName()
        {
            if (SspiNegotiation != null && !IsNegotiationCompleted)
            {
                return SspiNegotiation.GetRemoteIdentityName();
            }
            return base.GetRemoteIdentityName();
        }

        public override void Dispose()
        {
            try
            {
                using (AsyncLock.TakeLock())
                {
                    if (SspiNegotiation != null)
                    {
                        SspiNegotiation.Dispose();

                    }
                    if (NegotiationDigest != null)
                    {
                        ((IDisposable)NegotiationDigest).Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose();
            }
        }
    }
}
