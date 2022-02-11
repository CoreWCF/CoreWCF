// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public sealed class PolicyVersion
    {
        PolicyVersion(string policyNamespace)
        {
            Namespace = policyNamespace;
        }

        public static PolicyVersion Policy12 { get; } = new PolicyVersion(MetadataStrings.WSPolicy.NamespaceUri);
        public static PolicyVersion Policy15 { get; } = new PolicyVersion(MetadataStrings.WSPolicy.NamespaceUri15);
        public static PolicyVersion Default { get { return Policy12; } }

        public string Namespace { get; }

        public override string ToString() => Namespace;
    }
}
