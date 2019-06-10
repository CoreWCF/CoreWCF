using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace CoreWCF.Security.Tokens
{
    class WindowsSidIdentity : IIdentity
    {
        string _name;

        public WindowsSidIdentity(SecurityIdentifier sid)
        {
            SecurityIdentifier = sid ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(sid));
            AuthenticationType = string.Empty;
        }

        public WindowsSidIdentity(SecurityIdentifier sid, string name, string authenticationType)
        {
            SecurityIdentifier = sid ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(sid));
            _name = name ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            AuthenticationType = authenticationType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authenticationType));
        }

        public SecurityIdentifier SecurityIdentifier { get; }

        public string AuthenticationType { get; }

        public bool IsAuthenticated
        {
            get { return true; }
        }

        public string Name
        {
            get
            {
                if (_name == null)
                    _name = ((NTAccount)SecurityIdentifier.Translate(typeof(NTAccount))).Value;
                return _name;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            var sidIdentity = obj as WindowsSidIdentity;
            if (sidIdentity == null)
                return false;

            return SecurityIdentifier == sidIdentity.SecurityIdentifier;
        }

        public override int GetHashCode()
        {
            return SecurityIdentifier.GetHashCode();
        }
    }
}
