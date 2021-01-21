// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security
{
    public abstract class SecurityPolicyVersion
    {
        private readonly String spNamespace;
        private readonly String prefix;

        internal SecurityPolicyVersion(String ns, String prefix)
        {
            this.spNamespace = ns;
            this.prefix = prefix;
        }

        public String Namespace
        {
            get
            {
                return this.spNamespace;
            }
        }

        public String Prefix
        {
            get
            {
                return this.prefix;
            }
        }

        public static SecurityPolicyVersion WSSecurityPolicy11
        {
            get { return WSSecurityPolicyVersion11.Instance; }
        }

        public static SecurityPolicyVersion WSSecurityPolicy12
        {
            get { return WSSecurityPolicyVersion12.Instance; }
        }

        private class WSSecurityPolicyVersion11 : SecurityPolicyVersion
        {
            private static readonly WSSecurityPolicyVersion11 instance = new WSSecurityPolicyVersion11();

            protected WSSecurityPolicyVersion11()
                : base(CoreWCF.Security.WSSecurityPolicy11.WsspNamespace, WSSecurityPolicy.WsspPrefix)
            {
            }

            public static SecurityPolicyVersion Instance
            {
                get
                {
                    return instance;
                }
            }
        }

        private class WSSecurityPolicyVersion12 : SecurityPolicyVersion
        {
            private static readonly WSSecurityPolicyVersion12 instance = new WSSecurityPolicyVersion12();

            protected WSSecurityPolicyVersion12()
                : base(CoreWCF.Security.WSSecurityPolicy12.WsspNamespace, WSSecurityPolicy.WsspPrefix)
            {
            }

            public static SecurityPolicyVersion Instance
            {
                get
                {
                    return instance;
                }
            }
        }

    }
}
