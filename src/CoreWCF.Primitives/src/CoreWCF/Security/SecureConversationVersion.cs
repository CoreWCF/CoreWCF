// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Security
{
    public abstract class SecureConversationVersion
    {
        private readonly XmlDictionaryString prefix;

        internal SecureConversationVersion(XmlDictionaryString ns, XmlDictionaryString prefix)
        {
            Namespace = ns;
            this.prefix = prefix;
        }

        public XmlDictionaryString Namespace { get; }

        public XmlDictionaryString Prefix
        {
            get
            {
                return prefix;
            }
        }

        public static SecureConversationVersion Default
        {
            get { return WSSecureConversationFeb2005; }
        }

        public static SecureConversationVersion WSSecureConversationFeb2005
        {
            get { return WSSecureConversationVersionFeb2005.Instance; }
        }

        public static SecureConversationVersion WSSecureConversation13
        {
            get { return WSSecureConversationVersion13.Instance; }
        }

        private class WSSecureConversationVersionFeb2005 : SecureConversationVersion
        {
            private static readonly WSSecureConversationVersionFeb2005 instance = new WSSecureConversationVersionFeb2005();

            protected WSSecureConversationVersionFeb2005()
                : base(XD.SecureConversationFeb2005Dictionary.Namespace, XD.SecureConversationFeb2005Dictionary.Prefix)
            {
            }

            public static SecureConversationVersion Instance
            {
                get
                {
                    return instance;
                }
            }
        }

        private class WSSecureConversationVersion13 : SecureConversationVersion
        {
            private static readonly WSSecureConversationVersion13 instance = new WSSecureConversationVersion13();

            protected WSSecureConversationVersion13()
                : base(DXD.SecureConversationDec2005Dictionary.Namespace, DXD.SecureConversationDec2005Dictionary.Prefix)
            {
            }

            public static SecureConversationVersion Instance
            {
                get
                {
                    return instance;
                }
            }
        }
    }
}
