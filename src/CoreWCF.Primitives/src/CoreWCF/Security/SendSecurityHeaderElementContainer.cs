// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;
using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;

namespace CoreWCF.Security
{
    internal class SendSecurityHeaderElementContainer
    {
        private List<SecurityToken> signedSupportingTokens = null;
        private List<SendSecurityHeaderElement> basicSupportingTokens = null;
        private List<SecurityToken> endorsingSupportingTokens = null;
        private List<SecurityToken> endorsingDerivedSupportingTokens = null;
        private List<SecurityToken> signedEndorsingSupportingTokens = null;
        private List<SecurityToken> signedEndorsingDerivedSupportingTokens = null;
        private List<SendSecurityHeaderElement> signatureConfirmations = null;
        private List<SendSecurityHeaderElement> endorsingSignatures = null;
        private Dictionary<SecurityToken, SecurityKeyIdentifierClause> securityTokenMappedToIdentifierClause = null;

        public SecurityTimestamp Timestamp;
        public SecurityToken PrerequisiteToken;
        public SecurityToken SourceSigningToken;
        public SecurityToken DerivedSigningToken;
        public SecurityToken SourceEncryptionToken;
        public SecurityToken WrappedEncryptionToken;
        public SecurityToken DerivedEncryptionToken;
        public ISecurityElement ReferenceList;
        public SendSecurityHeaderElement PrimarySignature;

        private void Add<T>(ref List<T> list, T item)
        {
            if (list == null)
            {
                list = new List<T>();
            }
            list.Add(item);
        }

        public SecurityToken[] GetSignedSupportingTokens()
        {
            return (signedSupportingTokens != null) ? signedSupportingTokens.ToArray() : null;
        }

        public void AddSignedSupportingToken(SecurityToken token) => Add<SecurityToken>(ref signedSupportingTokens, token);

        public List<SecurityToken> EndorsingSupportingTokens => endorsingSupportingTokens;

        public SendSecurityHeaderElement[] GetBasicSupportingTokens() => (basicSupportingTokens != null) ? basicSupportingTokens.ToArray() : null;

        public void AddBasicSupportingToken(SendSecurityHeaderElement tokenElement) => Add<SendSecurityHeaderElement>(ref basicSupportingTokens, tokenElement);

        public SecurityToken[] GetSignedEndorsingSupportingTokens()
        {
            return (signedEndorsingSupportingTokens != null) ? signedEndorsingSupportingTokens.ToArray() : null;
        }

        public void AddSignedEndorsingSupportingToken(SecurityToken token)
        {
            Add<SecurityToken>(ref signedEndorsingSupportingTokens, token);
        }

        public SecurityToken[] GetSignedEndorsingDerivedSupportingTokens()
        {
            return (signedEndorsingDerivedSupportingTokens != null) ? signedEndorsingDerivedSupportingTokens.ToArray() : null;
        }

        public void AddSignedEndorsingDerivedSupportingToken(SecurityToken token)
        {
            Add<SecurityToken>(ref signedEndorsingDerivedSupportingTokens, token);
        }

        public SecurityToken[] GetEndorsingSupportingTokens() => (endorsingSupportingTokens != null) ? endorsingSupportingTokens.ToArray() : null;

        public void AddEndorsingSupportingToken(SecurityToken token) => Add<SecurityToken>(ref endorsingSupportingTokens, token);

        public SecurityToken[] GetEndorsingDerivedSupportingTokens() => (endorsingDerivedSupportingTokens != null) ? endorsingDerivedSupportingTokens.ToArray() : null;

        public void AddEndorsingDerivedSupportingToken(SecurityToken token) => Add<SecurityToken>(ref endorsingDerivedSupportingTokens, token);

        public SendSecurityHeaderElement[] GetSignatureConfirmations() => (signatureConfirmations != null) ? signatureConfirmations.ToArray() : null;

        public void AddSignatureConfirmation(SendSecurityHeaderElement confirmation) => Add<SendSecurityHeaderElement>(ref signatureConfirmations, confirmation);

        public SendSecurityHeaderElement[] GetEndorsingSignatures() => (endorsingSignatures != null) ? endorsingSignatures.ToArray() : null;

        public void AddEndorsingSignature(SendSecurityHeaderElement signature) => Add<SendSecurityHeaderElement>(ref endorsingSignatures, signature);

        public void MapSecurityTokenToStrClause(SecurityToken securityToken, SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (securityTokenMappedToIdentifierClause == null)
            {
                securityTokenMappedToIdentifierClause = new Dictionary<SecurityToken, SecurityKeyIdentifierClause>();
            }

            if (!securityTokenMappedToIdentifierClause.ContainsKey(securityToken))
            {
                securityTokenMappedToIdentifierClause.Add(securityToken, keyIdentifierClause);
            }
        }

        public bool TryGetIdentifierClauseFromSecurityToken(SecurityToken securityToken, out SecurityKeyIdentifierClause keyIdentifierClause)
        {
            keyIdentifierClause = null;
            if (securityToken == null
                || securityTokenMappedToIdentifierClause == null
                || !securityTokenMappedToIdentifierClause.TryGetValue(securityToken, out keyIdentifierClause))
            {
                return false;
            }
            return true;
        }
    }
}
