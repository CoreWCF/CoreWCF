// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    public abstract class BasicSecurityProfileVersion
    {
        internal BasicSecurityProfileVersion() { }

        public static BasicSecurityProfileVersion BasicSecurityProfile10
        {
            get { return BasicSecurityProfile10BasicSecurityProfileVersion.Instance; }
        }

        private class BasicSecurityProfile10BasicSecurityProfileVersion : BasicSecurityProfileVersion
        {
            private static BasicSecurityProfile10BasicSecurityProfileVersion instance = new BasicSecurityProfile10BasicSecurityProfileVersion();

            public static BasicSecurityProfile10BasicSecurityProfileVersion Instance { get { return instance; } }

            public override string ToString()
            {
                return "BasicSecurityProfile10";
            }
        }
    }
}
