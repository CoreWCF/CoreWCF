// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using CoreWCF.IdentityModel;
using UniqueId = System.Xml.UniqueId;

namespace CoreWCF.Security
{
    public class SecurityContextKeyIdentifierClause : SecurityKeyIdentifierClause
    {
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
            ContextId = contextId ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contextId));
            Generation = generation;
        }

        public UniqueId ContextId { get; }

        public UniqueId Generation { get; }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            SecurityContextKeyIdentifierClause that = keyIdentifierClause as SecurityContextKeyIdentifierClause;
            return ReferenceEquals(this, that) || (that != null && that.Matches(ContextId, Generation));
        }

        public bool Matches(UniqueId contextId, UniqueId generation)
        {
            return contextId == ContextId && generation == Generation;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "SecurityContextKeyIdentifierClause(ContextId = '{0}', Generation = '{1}')",
                ContextId, Generation);
        }
    }
}
