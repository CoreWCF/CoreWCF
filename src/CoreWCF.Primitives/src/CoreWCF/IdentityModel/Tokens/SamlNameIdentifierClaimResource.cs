// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SamlNameIdentifierClaimResource
    {
        [DataMember]
        private readonly string _nameQualifier;

        [DataMember]
        private readonly string _format;

        [DataMember]
        private readonly string _name;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext ctx)
        {
            if (string.IsNullOrEmpty(_name))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(_name));
        }

        public SamlNameIdentifierClaimResource(string name, string nameQualifier, string format)
        {
            if (string.IsNullOrEmpty(name))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(name));

            _name = name;
            _nameQualifier = nameQualifier;
            _format = format;
        }

        public string NameQualifier => _nameQualifier;

        public string Format => _format;

        public string Name => _name;

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            SamlNameIdentifierClaimResource rhs = obj as SamlNameIdentifierClaimResource;
            if (rhs == null)
                return false;

            return ((_nameQualifier == rhs._nameQualifier) && (_format == rhs._format) && (_name == rhs._name));
        }

        public override int GetHashCode()
        {
            return _name.GetHashCode();
        }
    }
}
