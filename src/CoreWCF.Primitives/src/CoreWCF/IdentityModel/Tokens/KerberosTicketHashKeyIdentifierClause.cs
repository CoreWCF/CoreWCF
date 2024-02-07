// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class KerberosTicketHashKeyIdentifierClause : BinaryKeyIdentifierClause
    {
        public KerberosTicketHashKeyIdentifierClause(byte[] ticketHash)
            : this(ticketHash, null, 0)
        {
        }

        public KerberosTicketHashKeyIdentifierClause(byte[] ticketHash, byte[] derivationNonce, int derivationLength)
            : this(ticketHash, true, derivationNonce, derivationLength)
        {
        }

        internal KerberosTicketHashKeyIdentifierClause(byte[] ticketHash, bool cloneBuffer, byte[] derivationNonce, int derivationLength)
            : base(null, ticketHash, cloneBuffer, derivationNonce, derivationLength)
        {
        }

        public byte[] GetKerberosTicketHash()
        {
            return GetBuffer();
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "KerberosTicketHashKeyIdentifierClause(Hash = {0})", ToBase64String());
        }
    }
}
