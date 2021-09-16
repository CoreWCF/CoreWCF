// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public sealed class PolicyVersion
    {
        static readonly PolicyVersion s_policyVersion12;

        static PolicyVersion()
        {
            s_policyVersion12 = new PolicyVersion(MetadataStrings.WSPolicy.NamespaceUri);
            Policy15 = new PolicyVersion(MetadataStrings.WSPolicy.NamespaceUri15);
        }

        PolicyVersion(string policyNamespace)
        {
            Namespace = policyNamespace;
        }

        public static PolicyVersion Policy12 { get { return s_policyVersion12; } }
        public static PolicyVersion Policy15 { get; private set; }
        public static PolicyVersion Default { get { return s_policyVersion12; } }
        public string Namespace { get; }

        public override string ToString()
        {
            return Namespace;
        }
    }
}
