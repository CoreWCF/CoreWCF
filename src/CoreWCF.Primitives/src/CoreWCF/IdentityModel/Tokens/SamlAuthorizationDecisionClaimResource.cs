// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/System.IdentityModel.Tokens")]
    public class SamlAuthorizationDecisionClaimResource
    {
        [DataMember]
        private string resource;

        [DataMember]
        private SamlAccessDecision accessDecision;

        [DataMember]
        private string actionNamespace;

        [DataMember]
        private string actionName;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext ctx)
        {
            if (string.IsNullOrEmpty(resource))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(resource));
            if (string.IsNullOrEmpty(actionName))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actionName));
        }

        public SamlAuthorizationDecisionClaimResource(string resource, SamlAccessDecision accessDecision, string actionNamespace, string actionName)
        {
            if (string.IsNullOrEmpty(resource))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(resource));
            if (string.IsNullOrEmpty(actionName))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actionName));

            this.resource = resource;
            this.accessDecision = accessDecision;
            this.actionNamespace = actionNamespace;
            this.actionName = actionName;
        }

        public string Resource => resource;

        public SamlAccessDecision AccessDecision => accessDecision;

        public string ActionNamespace => actionNamespace;

        public string ActionName => actionName;

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            SamlAuthorizationDecisionClaimResource rhs = obj as SamlAuthorizationDecisionClaimResource;
            if (rhs == null)
                return false;

            return ((ActionName == rhs.ActionName) && (ActionNamespace == rhs.ActionNamespace) &&
                (Resource == rhs.Resource) && (AccessDecision == rhs.AccessDecision));
        }

        public override int GetHashCode()
        {
            return (resource.GetHashCode() ^ accessDecision.GetHashCode());
        }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/System.IdentityModel.Tokens")]
    public enum SamlAccessDecision
    {
        [EnumMember]
        Permit,
        [EnumMember]
        Deny,
        [EnumMember]
        Indeterminate
    }
}
