using System.Globalization;
using CoreWCF.IdentityModel.Tokens;
using System.Runtime.CompilerServices;
using System.Xml;
using CoreWCF.IdentityModel;

namespace CoreWCF.Security
{
    public class SecurityContextKeyIdentifierClause : SecurityKeyIdentifierClause
    {
        readonly UniqueId contextId;
        readonly UniqueId generation;

        public SecurityContextKeyIdentifierClause(UniqueId contextId)
            : this(contextId, null)
        {
        }

        public SecurityContextKeyIdentifierClause(UniqueId contextId, UniqueId generation)
            : this(contextId, generation, null, 0)
        {
        }

        public SecurityContextKeyIdentifierClause(UniqueId contextId, UniqueId generation, byte[] derivationNonce, int derivationLength)
            : base(null, derivationNonce, derivationLength)
        {
            if (contextId == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("contextId");
            }
            this.contextId = contextId;
            this.generation = generation;
        }

        public UniqueId ContextId
        {
            get { return this.contextId; }
        }

        public UniqueId Generation
        {
            get { return this.generation; }
        }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            SecurityContextKeyIdentifierClause that = keyIdentifierClause as SecurityContextKeyIdentifierClause;

            // PreSharp Bug: Parameter 'that' to this public method must be validated: A null-dereference can occur here.
#pragma warning suppress 56506
            return ReferenceEquals(this, that) || (that != null && that.Matches(this.contextId, this.generation));
        }

        public bool Matches(UniqueId contextId, UniqueId generation)
        {
            return contextId == this.contextId && generation == this.generation;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "SecurityContextKeyIdentifierClause(ContextId = '{0}', Generation = '{1}')",
                this.ContextId, this.Generation);
        }
    }
}
