// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using SamlAuthorityBinding = Microsoft.IdentityModel.Tokens.Saml.SamlAuthorityBinding;

namespace CoreWCF.IdentityModel.Tokens
{
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/System.IdentityModel.Tokens")]
    public class SamlAuthenticationClaimResource
    {
        [DataMember]
        private DateTime authenticationInstant;
        [DataMember]
        private string authenticationMethod;
        [DataMember]
        private string dnsAddress;
        [DataMember]
        private string ipAddress;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext ctx)
        {
            if (string.IsNullOrEmpty(authenticationMethod))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authenticationMethod));
            if (AuthorityBindings == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(AuthorityBindings));
        }

        public SamlAuthenticationClaimResource(
            DateTime authenticationInstant,
            string authenticationMethod,
            string dnsAddress,
            string ipAddress
            )
        {
            if (string.IsNullOrEmpty(authenticationMethod))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authenticationMethod));

            this.authenticationInstant = authenticationInstant;
            this.authenticationMethod = authenticationMethod;
            this.dnsAddress = dnsAddress;
            this.ipAddress = ipAddress;
            AuthorityBindings = (new List<SamlAuthorityBinding>()).AsReadOnly();
        }

        public SamlAuthenticationClaimResource(
            DateTime authenticationInstant,
            string authenticationMethod,
            string dnsAddress,
            string ipAddress,
            IEnumerable<SamlAuthorityBinding> authorityBindings
            )
            : this(authenticationInstant, authenticationMethod, dnsAddress, ipAddress)
        {
            if (authorityBindings == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(authorityBindings)));

            List<SamlAuthorityBinding> tempList = new List<SamlAuthorityBinding>();
            foreach (SamlAuthorityBinding authorityBinding in authorityBindings)
            {
                if (authorityBinding != null)
                    tempList.Add(authorityBinding);
            }
            AuthorityBindings = tempList.AsReadOnly();

        }

        public SamlAuthenticationClaimResource(
            DateTime authenticationInstant,
            string authenticationMethod,
            string dnsAddress,
            string ipAddress,
            ReadOnlyCollection<SamlAuthorityBinding> authorityBindings
            )
            : this(authenticationInstant, authenticationMethod, dnsAddress, ipAddress)
        {
            AuthorityBindings = authorityBindings ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(authorityBindings)));

        }

        public DateTime AuthenticationInstant => authenticationInstant;

        public string AuthenticationMethod => authenticationMethod;

        public ReadOnlyCollection<SamlAuthorityBinding> AuthorityBindings { get; private set; }

        [DataMember]
        private List<SamlAuthorityBinding> SamlAuthorityBindings
        {
            get
            {
                List<SamlAuthorityBinding> sab = new List<SamlAuthorityBinding>();
                for (int i = 0; i < AuthorityBindings.Count; ++i)
                {
                    sab.Add(AuthorityBindings[i]);
                }
                return sab;
            }
            set
            {
                if (value != null)
                    AuthorityBindings = value.AsReadOnly();
            }
        }

        public string IPAddress => ipAddress;

        public string DnsAddress => dnsAddress;

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (!(obj is SamlAuthenticationClaimResource rhs))
                return false;

            if ((AuthenticationInstant != rhs.AuthenticationInstant) ||
                (AuthenticationMethod != rhs.AuthenticationMethod) ||
                (AuthorityBindings.Count != rhs.AuthorityBindings.Count) ||
                (IPAddress != rhs.IPAddress) ||
                (DnsAddress != rhs.DnsAddress))
                return false;

            int i;
            for (i = 0; i < AuthorityBindings.Count; ++i)
            {
                bool matched = false;
                for (int j = 0; j < rhs.AuthorityBindings.Count; ++j)
                {
                    if ((AuthorityBindings[i].AuthorityKind == rhs.AuthorityBindings[j].AuthorityKind) &&
                        (AuthorityBindings[i].Binding == rhs.AuthorityBindings[j].Binding) &&
                        (AuthorityBindings[i].Location == rhs.AuthorityBindings[j].Location))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return (authenticationInstant.GetHashCode() ^ authenticationMethod.GetHashCode());
        }
    }
}
