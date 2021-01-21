// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Xml;
using CoreWCF.IdentityModel;

namespace CoreWCF.Security
{
    public class SecurityContextKeyIdentifierClause : SecurityKeyIdentifierClause
    {
        private readonly UniqueId generation;

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
            this.ContextId = contextId;
            this.generation = generation;
        }

        public UniqueId ContextId { get; }

        public UniqueId Generation
        {
            get { return this.generation; }
        }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            SecurityContextKeyIdentifierClause that = keyIdentifierClause as SecurityContextKeyIdentifierClause;
            return ReferenceEquals(this, that) || (that != null && that.Matches(this.ContextId, this.generation));
        }

        public bool Matches(UniqueId contextId, UniqueId generation)
        {
            return contextId == this.ContextId && generation == this.generation;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "SecurityContextKeyIdentifierClause(ContextId = '{0}', Generation = '{1}')",
                this.ContextId, this.Generation);
        }
    }
}
