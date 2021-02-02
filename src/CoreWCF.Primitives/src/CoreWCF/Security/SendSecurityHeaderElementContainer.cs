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
        private List<SecurityToken> _signedSupportingTokens = null;
        private List<SendSecurityHeaderElement> _basicSupportingTokens = null;
        private List<SecurityToken> _endorsingSupportingTokens = null;
        private List<SecurityToken> _endorsingDerivedSupportingTokens = null;
        private List<SecurityToken> _signedEndorsingSupportingTokens = null;
        private List<SecurityToken> _signedEndorsingDerivedSupportingTokens = null;
        private List<SendSecurityHeaderElement> _signatureConfirmations = null;
        private List<SendSecurityHeaderElement> _endorsingSignatures = null;
        private Dictionary<SecurityToken, SecurityKeyIdentifierClause> _securityTokenMappedToIdentifierClause = null;

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
            return _signedSupportingTokens?.ToArray();
        }

        public void AddSignedSupportingToken(SecurityToken token) => Add<SecurityToken>(ref _signedSupportingTokens, token);

        public List<SecurityToken> EndorsingSupportingTokens => _endorsingSupportingTokens;

        public SendSecurityHeaderElement[] GetBasicSupportingTokens() => _basicSupportingTokens?.ToArray();

        public void AddBasicSupportingToken(SendSecurityHeaderElement tokenElement) => Add<SendSecurityHeaderElement>(ref _basicSupportingTokens, tokenElement);

        public SecurityToken[] GetSignedEndorsingSupportingTokens()
        {
            return _signedEndorsingSupportingTokens?.ToArray();
        }

        public void AddSignedEndorsingSupportingToken(SecurityToken token)
        {
            Add<SecurityToken>(ref _signedEndorsingSupportingTokens, token);
        }

        public SecurityToken[] GetSignedEndorsingDerivedSupportingTokens()
        {
            return _signedEndorsingDerivedSupportingTokens?.ToArray();
        }

        public void AddSignedEndorsingDerivedSupportingToken(SecurityToken token)
        {
            Add<SecurityToken>(ref _signedEndorsingDerivedSupportingTokens, token);
        }

        public SecurityToken[] GetEndorsingSupportingTokens() => _endorsingSupportingTokens?.ToArray();

        public void AddEndorsingSupportingToken(SecurityToken token) => Add<SecurityToken>(ref _endorsingSupportingTokens, token);

        public SecurityToken[] GetEndorsingDerivedSupportingTokens() => _endorsingDerivedSupportingTokens?.ToArray();

        public void AddEndorsingDerivedSupportingToken(SecurityToken token) => Add<SecurityToken>(ref _endorsingDerivedSupportingTokens, token);

        public SendSecurityHeaderElement[] GetSignatureConfirmations() => _signatureConfirmations?.ToArray();

        public void AddSignatureConfirmation(SendSecurityHeaderElement confirmation) => Add<SendSecurityHeaderElement>(ref _signatureConfirmations, confirmation);

        public SendSecurityHeaderElement[] GetEndorsingSignatures() => _endorsingSignatures?.ToArray();

        public void AddEndorsingSignature(SendSecurityHeaderElement signature) => Add<SendSecurityHeaderElement>(ref _endorsingSignatures, signature);

        public void MapSecurityTokenToStrClause(SecurityToken securityToken, SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (_securityTokenMappedToIdentifierClause == null)
            {
                _securityTokenMappedToIdentifierClause = new Dictionary<SecurityToken, SecurityKeyIdentifierClause>();
            }

            if (!_securityTokenMappedToIdentifierClause.ContainsKey(securityToken))
            {
                _securityTokenMappedToIdentifierClause.Add(securityToken, keyIdentifierClause);
            }
        }

        public bool TryGetIdentifierClauseFromSecurityToken(SecurityToken securityToken, out SecurityKeyIdentifierClause keyIdentifierClause)
        {
            keyIdentifierClause = null;
            if (securityToken == null
                || _securityTokenMappedToIdentifierClause == null
                || !_securityTokenMappedToIdentifierClause.TryGetValue(securityToken, out keyIdentifierClause))
            {
                return false;
            }
            return true;
        }
    }
}
