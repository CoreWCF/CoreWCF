// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;
using CoreWCF.Security.NegotiateInternal;

namespace CoreWCF.Security
{
    /// <summary>
    /// WindowsSspiNegotiation is re-write of 
    /// https://referencesource.microsoft.com/#System.ServiceModel/System/ServiceModel/Security/WindowsSspiNegotiation.cs
    /// With .net standard, many code moved to NTAuth/NegotiateStream.. 
    /// Using another layer of abstraction in NegotiateInternal.NegotiateInternalState to call most of the API using reflection.
    /// 
    /// </summary>
    internal class WindowsSspiNegotiation : ISspiNegotiation
    {
        private readonly INegotiateInternalState _negotiateState;

        public WindowsSspiNegotiation(INegotiateInternalState passedNegotiateState)
        {
            _negotiateState = passedNegotiateState;
        }

        public bool IsCompleted { get; private set; }

        public bool IsValidContext => _negotiateState.IsValidContext;

        public string KeyEncryptionAlgorithm => SecurityAlgorithmStrings.WindowsSspiKeyWrap;

        public byte[] Decrypt(byte[] encryptedData) => throw new NotImplementedException();

        public void Dispose()
        {
            if (_negotiateState != null)
            {
                _negotiateState.Dispose();
            }
        }

        public byte[] Encrypt(byte[] input) => _negotiateState.Encrypt(input);

        public byte[] GetOutgoingBlob(byte[] incomingBlob, ChannelBinding channelbinding, ExtendedProtectionPolicy protectionPolicy)
        {
            byte[] outGoingBlob = _negotiateState.GetOutgoingBlob(incomingBlob, out BlobErrorType errorType, out Exception exception);
            if (errorType != BlobErrorType.None)
            {
                throw exception;
            }
            IsCompleted = _negotiateState.IsCompleted;
            return outGoingBlob;
        }

        public string GetRemoteIdentityName()
        {
            if (IsValidContext)
            {
                IIdentity identity = _negotiateState.GetIdentity();
                if (identity != null)
                {
                    return identity.Name;
                }
            }
            return string.Empty;
        }

        public IIdentity GetIdentity()
        {
            if (IsValidContext)
            {
                return _negotiateState.GetIdentity();
            }

            return null;
        }
    }
}
