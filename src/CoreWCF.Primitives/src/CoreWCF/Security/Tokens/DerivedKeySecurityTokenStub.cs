using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;
using System;
using System.Collections.ObjectModel;

namespace CoreWCF.Security.Tokens
{
    internal sealed class DerivedKeySecurityTokenStub : SecurityToken
    {
        private string id;
        private string derivationAlgorithm;
        private string label;
        private int length;
        private byte[] nonce;
        private int offset;
        private int generation;
        private SecurityKeyIdentifierClause tokenToDeriveIdentifier;

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

        public override string Id => this.id;

        public override DateTime ValidFrom => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());

        public override DateTime ValidTo => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => null;

        public SecurityKeyIdentifierClause TokenToDeriveIdentifier => this.tokenToDeriveIdentifier;

        public DerivedKeySecurityToken CreateToken(SecurityToken tokenToDerive, int maxKeyLength)
        {
            DerivedKeySecurityToken result = new DerivedKeySecurityToken(this.generation, this.offset, this.length,
                this.label, this.nonce, tokenToDerive, this.tokenToDeriveIdentifier, this.derivationAlgorithm, this.Id);
            result.InitializeDerivedKey(maxKeyLength);
            return result;
        }
    }
}
