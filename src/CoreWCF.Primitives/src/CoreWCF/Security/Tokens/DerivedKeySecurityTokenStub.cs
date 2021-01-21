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
        private readonly string id;
        private readonly string derivationAlgorithm;
        private readonly string label;
        private readonly int length;
        private readonly byte[] nonce;
        private readonly int offset;
        private readonly int generation;
        private readonly SecurityKeyIdentifierClause tokenToDeriveIdentifier;

        public DerivedKeySecurityTokenStub(int generation, int offset, int length,
            string label, byte[] nonce,
            SecurityKeyIdentifierClause tokenToDeriveIdentifier, string derivationAlgorithm, string id)
        {
            this.id = id;
            this.generation = generation;
            this.offset = offset;
            this.length = length;
            this.label = label;
            this.nonce = nonce;
            this.tokenToDeriveIdentifier = tokenToDeriveIdentifier;
            this.derivationAlgorithm = derivationAlgorithm;
        }

        public override string Id => id;

        public override DateTime ValidFrom => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());

        public override DateTime ValidTo => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => null;

        public SecurityKeyIdentifierClause TokenToDeriveIdentifier => tokenToDeriveIdentifier;

        public DerivedKeySecurityToken CreateToken(SecurityToken tokenToDerive, int maxKeyLength)
        {
            DerivedKeySecurityToken result = new DerivedKeySecurityToken(generation, offset, length,
                label, nonce, tokenToDerive, tokenToDeriveIdentifier, derivationAlgorithm, Id);
            result.InitializeDerivedKey(maxKeyLength);
            return result;
        }
    }
}
