// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class X509WindowsSecurityToken : X509SecurityToken
    {
        private WindowsIdentity windowsIdentity;
        private bool disposed = false;

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
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(windowsIdentity));
            }

            AuthenticationType = authenticationType;
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

        public string AuthenticationType { get; }

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
