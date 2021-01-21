// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    public class X509SecurityToken : SecurityToken, IDisposable
    {
        private string id;
        private X509Certificate2 certificate;
        private ReadOnlyCollection<SecurityKey> securityKeys;
        private DateTime effectiveTime = SecurityUtils.MaxUtcDateTime;
        private DateTime expirationTime = SecurityUtils.MinUtcDateTime;
        private bool disposed = false;
        private bool disposable;

        public X509SecurityToken(X509Certificate2 certificate)
            : this(certificate, SecurityUniqueId.Create().Value)
        {
        }

        public X509SecurityToken(X509Certificate2 certificate, string id)
            : this(certificate, id, true)
        {
        }

        internal X509SecurityToken(X509Certificate2 certificate, bool clone)
            : this(certificate, SecurityUniqueId.Create().Value, clone)
        {
        }

        internal X509SecurityToken(X509Certificate2 certificate, bool clone, bool disposable)
            : this(certificate, SecurityUniqueId.Create().Value, clone, disposable)
        {
        }

        internal X509SecurityToken(X509Certificate2 certificate, string id, bool clone)
            : this(certificate, id, clone, true)
        {
        }

        internal X509SecurityToken(X509Certificate2 certificate, string id, bool clone, bool disposable)
        {
            if (certificate == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            if (id == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));

            this.id = id;
            this.certificate = clone ? new X509Certificate2(certificate) : certificate;
            // if the cert needs to be cloned then the token owns the clone and should dispose it
            this.disposable = clone || disposable;
        }

        public override string Id
        {
            get { return id; }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get
            {
                ThrowIfDisposed();
                if (this.securityKeys == null)
                {
                    List<SecurityKey> temp = new List<SecurityKey>(1);
                    temp.Add(new X509AsymmetricSecurityKey(this.certificate));
                    this.securityKeys = temp.AsReadOnly();
                }
                return this.securityKeys;
            }
        }

        public override DateTime ValidFrom
        {
            get
            {
                ThrowIfDisposed();
                if (effectiveTime == SecurityUtils.MaxUtcDateTime)
                    effectiveTime = certificate.NotBefore.ToUniversalTime();
                return effectiveTime;
            }
        }

        public override DateTime ValidTo
        {
            get
            {
                ThrowIfDisposed();
                if (expirationTime == SecurityUtils.MinUtcDateTime)
                    expirationTime = certificate.NotAfter.ToUniversalTime();
                return expirationTime;
            }
        }

        public X509Certificate2 Certificate
        {
            get
            {
                ThrowIfDisposed();
                return certificate;
            }
        }

        public virtual void Dispose()
        {
            if (disposable && !disposed)
            {
                disposed = true;
                certificate.Reset();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }

}
