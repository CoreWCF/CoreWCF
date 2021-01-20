using CoreWCF.Security.NegotiateInternal;
using System;
using System.Collections.Generic;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Net.Security;
using System.IO;
using System.Security.Principal;

namespace CoreWCF.Security
{
    /// <summary>
    /// WindowsSspiNegotiation is re-write of 
    /// https://referencesource.microsoft.com/#System.ServiceModel/System/ServiceModel/Security/WindowsSspiNegotiation.cs
    /// With .net standard, many code moved to NTAuth/NegotiateStream.. 
    /// Using another layer of abstraction in NegotiateInternal.NegotiateInternalState to call most of the API using reflection.
    /// 
    /// </summary>
    class WindowsSspiNegotiation : ISspiNegotiation
    {
        private string package;
        private string defaultServiceBinding;
        NegotiateInternalState negotiateState;
        bool isCompleted;

        public WindowsSspiNegotiation(string package, string defaultServiceBinding, NegotiateInternalState passedNegotiateState )
        {
            this.package = package;
            this.defaultServiceBinding = defaultServiceBinding;
            this.negotiateState = passedNegotiateState;
        }

        //TODO incorporate
        //public DateTime ExpirationTimeUtc
        //{
        //    get
        //    {
        //       // ThrowIfDisposed();
        //        // if (this.LifeSpan == null)
        //        {
        //            return SecurityUtils.MaxUtcDateTime;
        //        }
        //        //  else
        //        // {
        //        //      return this.LifeSpan.ExpiryTimeUtc;
        //        //  }
        //    }
        //}

        public bool IsCompleted => this.isCompleted;

        public bool IsValidContext => this.negotiateState.IsValidContext;

        public string KeyEncryptionAlgorithm
        {
            get
            {
                return SecurityAlgorithmStrings.WindowsSspiKeyWrap;
            }
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (this.negotiateState != null)
                this.negotiateState.Dispose();
        }

        public byte[] Encrypt(byte[] input)
        {
            return this.negotiateState.Encrypt(input);

        }

        public byte[] GetOutgoingBlob(byte[] incomingBlob, ChannelBinding channelbinding, ExtendedProtectionPolicy protectionPolicy)
        {
            NegotiateInternal.BlobErrorType errorType;
            Exception exception;
            var outGoingBlob = this.negotiateState.GetOutgoingBlob(incomingBlob, out errorType, out exception);
            if (errorType != BlobErrorType.None)
            {
                throw exception;
            }
            this.isCompleted = this.negotiateState.IsCompleted;
            return outGoingBlob;
        }

        public string GetRemoteIdentityName()
        {
            if (IsValidContext)
            {
                IIdentity identity = this.negotiateState.GetIdentity();
                if (identity != null)
                    return identity.Name;
            }
            return string.Empty;
        }
        public IIdentity GetIdentity()
        {
            if (IsValidContext)
                return this.negotiateState.GetIdentity();
            return null;
        }
    }
}
