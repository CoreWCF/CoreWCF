// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    public class ClaimTypeRequirement
    {
        internal const bool DefaultIsOptional = false;
        private readonly string claimType;
        private readonly bool isOptional;

        public ClaimTypeRequirement(string claimType)
            : this(claimType, DefaultIsOptional)
        {
        }

        public ClaimTypeRequirement(string claimType, bool isOptional)
        {
            if (claimType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("claimType");
            }
            if (claimType.Length <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("claimType", SR.ClaimTypeCannotBeEmpty);
            }

            this.claimType = claimType;
            this.isOptional = isOptional;
        }

        public string ClaimType
        {
            get { return claimType; }
        }

        public bool IsOptional
        {
            get { return isOptional; }
        }
    }
}
