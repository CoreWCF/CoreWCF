using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Security.Tokens
{
    internal sealed class InitiatorServiceModelSecurityTokenRequirement : ServiceModelSecurityTokenRequirement
    {
        //WebHeaderCollection webHeaderCollection;

        public InitiatorServiceModelSecurityTokenRequirement()
            : base()
        {
            Properties.Add(IsInitiatorProperty, (object)true);
        }

        public EndpointAddress TargetAddress
        {
            get
            {
                return GetPropertyOrDefault<EndpointAddress>(TargetAddressProperty, null);
            }
            set
            {
                Properties[TargetAddressProperty] = value;
            }
        }

        public Uri Via
        {
            get
            {
                return GetPropertyOrDefault<Uri>(ViaProperty, null);
            }
            set
            {
                Properties[ViaProperty] = value;
            }
        }

        internal bool IsOutOfBandToken
        {
            get
            {
                return GetPropertyOrDefault<bool>(IsOutOfBandTokenProperty, false);
            }
            set
            {
                Properties[IsOutOfBandTokenProperty] = value;
            }
        }

        internal bool PreferSslCertificateAuthenticator
        {
            get
            {
                return GetPropertyOrDefault<bool>(PreferSslCertificateAuthenticatorProperty, false);
            }
            set
            {
                Properties[PreferSslCertificateAuthenticatorProperty] = value;
            }
        }

        //internal WebHeaderCollection WebHeaders
        //{
        //    get
        //    {
        //        return this.webHeaderCollection;
        //    }
        //    set
        //    {
        //        this.webHeaderCollection = value;
        //    }
        //}

        public override string ToString()
        {
            return InternalToString();
        }
    }

}
