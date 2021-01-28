// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel
{
    // All subclasses are required to be thread-safe and immutable

    // Self-resolving clauses such as RSA and X509 raw data should
    // override CanCreateKey and return true, and implement
    // CreateKey()

    public abstract class SecurityKeyIdentifierClause
    {
        private readonly byte[] _derivationNonce;
        private string _id = null;

        protected SecurityKeyIdentifierClause(string clauseType)
            : this(clauseType, null, 0)
        {
        }

        protected SecurityKeyIdentifierClause(string clauseType, byte[] nonce, int length)
        {
            ClauseType = clauseType;
            _derivationNonce = nonce;
            DerivationLength = length;
        }

        public virtual bool CanCreateKey
        {
            get { return false; }
        }

        public string ClauseType { get; }

        public string Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public virtual SecurityKey CreateKey()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.KeyIdentifierClauseDoesNotSupportKeyCreation));
        }

        public virtual bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            return ReferenceEquals(this, keyIdentifierClause);
        }

        public byte[] GetDerivationNonce()
        {
            return (_derivationNonce != null) ? (byte[])_derivationNonce.Clone() : null;
        }

        public int DerivationLength { get; }
    }
}
