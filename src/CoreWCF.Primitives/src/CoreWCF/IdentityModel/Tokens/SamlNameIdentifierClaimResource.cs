// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    [DataContract]
    public class SamlNameIdentifierClaimResource
    {
        [DataMember]
        private readonly string nameQualifier;

        [DataMember]
        private readonly string format;

        [DataMember]
        private readonly string name;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext ctx)
        {
            if (string.IsNullOrEmpty(name))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(name));
        }

        public SamlNameIdentifierClaimResource(string name, string nameQualifier, string format)
        {
            if (string.IsNullOrEmpty(name))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(name));

            this.name = name;
            this.nameQualifier = nameQualifier;
            this.format = format;
        }

        public string NameQualifier => nameQualifier;

        public string Format => format;

        public string Name => name;

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            SamlNameIdentifierClaimResource rhs = obj as SamlNameIdentifierClaimResource;
            if (rhs == null)
                return false;

            return ((nameQualifier == rhs.nameQualifier) && (format == rhs.format) && (name == rhs.name));
        }

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }
    }
}
