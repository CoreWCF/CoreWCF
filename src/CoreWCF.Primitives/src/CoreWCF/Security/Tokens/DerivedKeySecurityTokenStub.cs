// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    internal sealed class DerivedKeySecurityTokenStub : SecurityToken
    {
        private readonly string _id;
        private readonly string _derivationAlgorithm;
        private readonly string _label;
        private readonly int _length;
        private readonly byte[] _nonce;
        private readonly int _offset;
        private readonly int _generation;

        public DerivedKeySecurityTokenStub(int generation, int offset, int length,
            string label, byte[] nonce,
            SecurityKeyIdentifierClause tokenToDeriveIdentifier, string derivationAlgorithm, string id)
        {
            _id = id;
            _generation = generation;
            _offset = offset;
            _length = length;
            _label = label;
            _nonce = nonce;
            TokenToDeriveIdentifier = tokenToDeriveIdentifier;
            _derivationAlgorithm = derivationAlgorithm;
        }

        public override string Id => _id;

        public override DateTime ValidFrom => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());

        public override DateTime ValidTo => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => null;

        public SecurityKeyIdentifierClause TokenToDeriveIdentifier { get; }

        public DerivedKeySecurityToken CreateToken(SecurityToken tokenToDerive, int maxKeyLength)
        {
            DerivedKeySecurityToken result = new DerivedKeySecurityToken(_generation, _offset, _length,
                _label, _nonce, tokenToDerive, TokenToDeriveIdentifier, _derivationAlgorithm, Id);
            result.InitializeDerivedKey(maxKeyLength);
            return result;
        }
    }
}
