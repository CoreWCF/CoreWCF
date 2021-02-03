// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Security
{
    public abstract class TrustVersion
    {
        internal TrustVersion(XmlDictionaryString ns, XmlDictionaryString prefix)
        {
            Namespace = ns;
            Prefix = prefix;
        }

        public XmlDictionaryString Namespace { get; }

        public XmlDictionaryString Prefix { get; }

        public static TrustVersion Default => WSTrustFeb2005;

        public static TrustVersion WSTrustFeb2005 => WSTrustVersionFeb2005.Instance;

        public static TrustVersion WSTrust13 => WSTrustVersion13.Instance;

        private class WSTrustVersionFeb2005 : TrustVersion
        {
            private static readonly WSTrustVersionFeb2005 s_instance = new WSTrustVersionFeb2005();

            protected WSTrustVersionFeb2005()
                : base(XD.TrustFeb2005Dictionary.Namespace, XD.TrustFeb2005Dictionary.Prefix)
            {
            }

            public static TrustVersion Instance => s_instance;
        }

        private class WSTrustVersion13 : TrustVersion
        {
            private static readonly WSTrustVersion13 s_instance = new WSTrustVersion13();

            protected WSTrustVersion13()
                : base(DXD.TrustDec2005Dictionary.Namespace, DXD.TrustDec2005Dictionary.Prefix)
            {
            }

            public static TrustVersion Instance => s_instance;
        }
    }
}
