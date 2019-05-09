using Microsoft.ServiceModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.IdentityModel.Tokens
{
    internal class X509SecurityToken : SecurityToken, IDisposable
    {
        string id;
        X509Certificate2 certificate;
        DateTime effectiveTime = SecurityUtils.MaxUtcDateTime;
        DateTime expirationTime = SecurityUtils.MinUtcDateTime;
        bool disposed = false;
        bool disposable;

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("certificate");
            if (id == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("id");

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
                // I believe this is only used in MessageSecurity
                throw new PlatformNotSupportedException("X509AsymmetricSecurityKey");
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
