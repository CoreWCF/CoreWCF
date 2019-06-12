using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace CoreWCF.Security
{
    public abstract class SecurityVersion
    {
        internal SecurityVersion(XmlDictionaryString headerName, XmlDictionaryString headerNamespace, XmlDictionaryString headerPrefix)
        {
            HeaderName = headerName;
            HeaderNamespace = headerNamespace;
            HeaderPrefix = headerPrefix;
        }

        internal XmlDictionaryString HeaderName { get; }

        internal XmlDictionaryString HeaderNamespace { get; }

        internal XmlDictionaryString HeaderPrefix { get; }

        internal abstract XmlDictionaryString FailedAuthenticationFaultCode
        {
            get;
        }

        internal abstract XmlDictionaryString InvalidSecurityFaultCode
        {
            get;
        }

        public static SecurityVersion WSSecurity11
        {
            get { return SecurityVersion11.Instance; }
        }

        internal static SecurityVersion Default
        {
            get { return WSSecurity11; }
        }

        class SecurityVersion10 : SecurityVersion
        {
            static readonly SecurityVersion10 instance = new SecurityVersion10();

            protected SecurityVersion10()
                : base(XD.SecurityJan2004Dictionary.Security, XD.SecurityJan2004Dictionary.Namespace, XD.SecurityJan2004Dictionary.Prefix)
            {
            }

            public static SecurityVersion10 Instance
            {
                get { return instance; }
            }

            internal override XmlDictionaryString FailedAuthenticationFaultCode => XD.SecurityJan2004Dictionary.FailedAuthenticationFaultCode;

            internal override XmlDictionaryString InvalidSecurityFaultCode => XD.SecurityJan2004Dictionary.InvalidSecurityFaultCode;
        }

        sealed class SecurityVersion11 : SecurityVersion10
        {
            static readonly SecurityVersion11 instance = new SecurityVersion11();

            SecurityVersion11() : base()
            {
            }

            public new static SecurityVersion11 Instance
            {
                get { return instance; }
            }
        }
    }
}
