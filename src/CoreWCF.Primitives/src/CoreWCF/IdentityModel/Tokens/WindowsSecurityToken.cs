// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Security.Principal;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    public class WindowsSecurityToken : SecurityToken, IDisposable
    {
        private string _id;
        private DateTime _effectiveTime;
        private DateTime _expirationTime;
        private WindowsIdentity _windowsIdentity;
        private bool _disposed = false;

        public WindowsSecurityToken(WindowsIdentity windowsIdentity)
            : this(windowsIdentity, SecurityUniqueId.Create().Value)
        {
        }

        public WindowsSecurityToken(WindowsIdentity windowsIdentity, string id)
            : this(windowsIdentity, id, null)
        {
        }

        public WindowsSecurityToken(WindowsIdentity windowsIdentity, string id, string authenticationType)
        {
            DateTime effectiveTime = DateTime.UtcNow;
            Initialize(id, authenticationType, effectiveTime, DateTime.UtcNow.AddHours(10), windowsIdentity, true);
        }

        protected WindowsSecurityToken()
        {
        }

        protected void Initialize(string id, DateTime effectiveTime, DateTime expirationTime, WindowsIdentity windowsIdentity, bool clone)
        {
            Initialize(id, null, effectiveTime, expirationTime, windowsIdentity, clone);
        }

        protected void Initialize(string id, string authenticationType, DateTime effectiveTime, DateTime expirationTime, WindowsIdentity windowsIdentity, bool clone)
        {
            if (windowsIdentity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(windowsIdentity));
            }

            _id = id ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            AuthenticationType = authenticationType;
            _effectiveTime = effectiveTime;
            _expirationTime = expirationTime;
            _windowsIdentity = clone ? SecurityUtils.CloneWindowsIdentityIfNecessary(windowsIdentity, authenticationType) : windowsIdentity;
        }

        public override string Id => _id;

        public string AuthenticationType { get; private set; }

        public override DateTime ValidFrom => _effectiveTime;

        public override DateTime ValidTo => _expirationTime;

        public virtual WindowsIdentity WindowsIdentity
        {
            get
            {
                ThrowIfDisposed();
                return _windowsIdentity;
            }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => EmptyReadOnlyCollection<SecurityKey>.Instance;

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_windowsIdentity != null)
                {
                    _windowsIdentity.Dispose();
                    _windowsIdentity = null;
                }
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }
}
