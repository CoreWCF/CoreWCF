// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    public class ClaimTypeRequirement
    {
        internal const bool DefaultIsOptional = false;

        public ClaimTypeRequirement(string claimType)
            : this(claimType, DefaultIsOptional)
        {
        }

        public ClaimTypeRequirement(string claimType, bool isOptional)
        {
            if (claimType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(claimType));
            }
            if (claimType.Length <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(claimType), SR.ClaimTypeCannotBeEmpty);
            }

            ClaimType = claimType;
            IsOptional = isOptional;
        }

        public string ClaimType { get; }

        public bool IsOptional { get; }
    }
}
