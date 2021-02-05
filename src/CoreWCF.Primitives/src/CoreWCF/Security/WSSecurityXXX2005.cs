// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using TokenEntry = CoreWCF.Security.WSSecurityTokenSerializer.TokenEntry;

namespace CoreWCF.Security
{
    internal class WSSecurityXXX2005 : WSSecurityJan2004
    {
        public WSSecurityXXX2005(WSSecurityTokenSerializer tokenSerializer, SamlSerializer samlSerializer)
            : base(tokenSerializer, samlSerializer)
        {
        }

        public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
        {
            PopulateJan2004TokenEntries(tokenEntryList);
            tokenEntryList.Add(new WSSecurityXXX2005.WrappedKeyTokenEntry(WSSecurityTokenSerializer));
           // tokenEntryList.Add(new WSSecurityXXX2005.SamlTokenEntry(this.WSSecurityTokenSerializer, this.SamlSerializer));
        }

        //private new class SamlTokenEntry : WSSecurityJan2004.SamlTokenEntry
        //{
        //    public SamlTokenEntry(
        //      WSSecurityTokenSerializer tokenSerializer,
        //      SamlSerializer samlSerializer)
        //      : base((SecurityTokenSerializer)tokenSerializer, samlSerializer)
        //    {
        //    }

        //    public override string TokenTypeUri
        //    {
        //        get
        //        {
        //            return "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1#SAMLV1.1";
        //        }
        //    }
        //}

        private new class WrappedKeyTokenEntry : WSSecurityJan2004.WrappedKeyTokenEntry
        {
            public WrappedKeyTokenEntry(WSSecurityTokenSerializer tokenSerializer)
              : base(tokenSerializer)
            {
            }

            public override string TokenTypeUri
            {
                get
                {
                    return "http://docs.oasis-open.org/wss/oasis-wss-soap-message-security-1.1#EncryptedKey";
                }
            }
        }
    }
}
