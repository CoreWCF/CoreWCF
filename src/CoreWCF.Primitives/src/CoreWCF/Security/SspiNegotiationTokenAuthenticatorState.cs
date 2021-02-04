// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Security.Cryptography;
using System;

namespace CoreWCF.Security
{
    internal class SspiNegotiationTokenAuthenticatorState : NegotiationTokenAuthenticatorState
    {
        private DataContractSerializer _appliesToSerializer;

        public SspiNegotiationTokenAuthenticatorState(ISspiNegotiation sspiNegotiation)
            : base()
        {
            if (sspiNegotiation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("sspiNegotiation");
            }
            this.SspiNegotiation = sspiNegotiation;
            this.NegotiationDigest = CryptoHelper.NewSha1HashAlgorithm();
        }

        public ISspiNegotiation SspiNegotiation { get; }

        internal int RequestedKeySize { get; set; }

        internal HashAlgorithm NegotiationDigest { get; }

        internal string Context { get; set; }

        internal EndpointAddress AppliesTo { get; set; }

        internal DataContractSerializer AppliesToSerializer
        {
            get
            {
                return this._appliesToSerializer;
            }
            set
            {
                this._appliesToSerializer = value;
            }
        }

        public override string GetRemoteIdentityName()
        {
            if (this.SspiNegotiation != null && !this.IsNegotiationCompleted)
            {
                return this.SspiNegotiation.GetRemoteIdentityName();
            }
            return base.GetRemoteIdentityName();
        }

        public override void Dispose()
        {
            try
            {
                lock (ThisLock)
                {
                    if (this.SspiNegotiation != null)
                    {
                        this.SspiNegotiation.Dispose();

                    }
                    if (this.NegotiationDigest != null)
                    {
                        ((IDisposable)this.NegotiationDigest).Dispose();
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
