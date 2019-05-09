using Microsoft.ServiceModel;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace Microsoft.IdentityModel.Tokens
{
    internal class X509WindowsSecurityToken : X509SecurityToken
    {
        WindowsIdentity windowsIdentity;
        bool disposed = false;
        string authenticationType;

        public X509WindowsSecurityToken(X509Certificate2 certificate, WindowsIdentity windowsIdentity)
            : this(certificate, windowsIdentity, null, true)
        {
        }

        public X509WindowsSecurityToken(X509Certificate2 certificate, WindowsIdentity windowsIdentity, string id)
            : this(certificate, windowsIdentity, null, id, true)
        {
        }

        public X509WindowsSecurityToken(X509Certificate2 certificate, WindowsIdentity windowsIdentity, string authenticationType, string id)
            : this(certificate, windowsIdentity, authenticationType, id, true)
        {
        }

        internal X509WindowsSecurityToken(X509Certificate2 certificate, WindowsIdentity windowsIdentity, string authenticationType, bool clone)
            : this(certificate, windowsIdentity, authenticationType, SecurityUniqueId.Create().Value, clone)
        {
        }

        internal X509WindowsSecurityToken(X509Certificate2 certificate, WindowsIdentity windowsIdentity, string authenticationType, string id, bool clone)
            : base(certificate, id, clone)
        {
            if (windowsIdentity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("windowsIdentity");

            this.authenticationType = authenticationType;
            this.windowsIdentity = clone ? SecurityUtils.CloneWindowsIdentityIfNecessary(windowsIdentity, authenticationType) : windowsIdentity;
        }


        public WindowsIdentity WindowsIdentity
        {
            get
            {
                ThrowIfDisposed();
                return windowsIdentity;
            }
        }

        public string AuthenticationType
        {
            get
            {
                return authenticationType;
            }
        }

        public override void Dispose()
        {
            try
            {
                if (!disposed)
                {
                    disposed = true;
                    windowsIdentity.Dispose();
                    windowsIdentity = null;
                }
            }
            finally
            {
                base.Dispose();
            }
        }
    }

}
