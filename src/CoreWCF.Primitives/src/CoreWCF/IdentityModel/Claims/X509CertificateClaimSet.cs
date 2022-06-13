﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Claims
{
    public class X509CertificateClaimSet : ClaimSet, IIdentityInfo, IDisposable
    {
        private readonly X509Certificate2 _certificate;
        private DateTime _expirationTime = SecurityUtils.MinUtcDateTime;
        private ClaimSet _issuer;
        private X509Identity _identity;
        private X509ChainElementCollection _elements;
        private IList<Claim> _claims;
        private int _index;
        private bool _disposed = false;

        public X509CertificateClaimSet(X509Certificate2 certificate)
            : this(certificate, true)
        {
        }

        internal X509CertificateClaimSet(X509Certificate2 certificate, bool clone)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            _certificate = clone ? new X509Certificate2(certificate) : certificate;
        }

        private X509CertificateClaimSet(X509CertificateClaimSet from)
            : this(from.X509Certificate, true)
        {
        }

        private X509CertificateClaimSet(X509ChainElementCollection elements, int index)
        {
            _elements = elements;
            _index = index;
            _certificate = elements[index].Certificate;
        }

        public override Claim this[int index]
        {
            get
            {
                ThrowIfDisposed();
                EnsureClaims();
                return _claims[index];
            }
        }

        public override int Count
        {
            get
            {
                ThrowIfDisposed();
                EnsureClaims();
                return _claims.Count;
            }
        }

        IIdentity IIdentityInfo.Identity
        {
            get
            {
                ThrowIfDisposed();
                if (_identity == null)
                {
                    _identity = new X509Identity(_certificate, false, false);
                }

                return _identity;
            }
        }

        public DateTime ExpirationTime
        {
            get
            {
                ThrowIfDisposed();
                if (_expirationTime == SecurityUtils.MinUtcDateTime)
                {
                    _expirationTime = _certificate.NotAfter.ToUniversalTime();
                }

                return _expirationTime;
            }
        }

        public override ClaimSet Issuer
        {
            get
            {
                ThrowIfDisposed();
                if (_issuer == null)
                {
                    if (_elements == null)
                    {
                        X509Chain chain = new X509Chain();
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        chain.Build(_certificate);
                        _index = 0;
                        _elements = chain.ChainElements;
                    }

                    if (_index + 1 < _elements.Count)
                    {
                        _issuer = new X509CertificateClaimSet(_elements, _index + 1);
                        _elements = null;
                    }
                    // SelfSigned?
                    else if (StringComparer.OrdinalIgnoreCase.Equals(_certificate.SubjectName.Name, _certificate.IssuerName.Name))
                    {
                        _issuer = this;
                    }
                    else
                    {
                        _issuer = new X500DistinguishedNameClaimSet(_certificate.IssuerName);
                    }
                }
                return _issuer;
            }
        }

        public X509Certificate2 X509Certificate
        {
            get
            {
                ThrowIfDisposed();
                return _certificate;
            }
        }

        internal X509CertificateClaimSet Clone()
        {
            ThrowIfDisposed();
            return new X509CertificateClaimSet(this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                SecurityUtils.DisposeIfNecessary(_identity);
                if (_issuer != null)
                {
                    if (_issuer != this)
                    {
                        SecurityUtils.DisposeIfNecessary(_issuer as IDisposable);
                    }
                }
                if (_elements != null)
                {
                    for (int i = _index + 1; i < _elements.Count; ++i)
                    {
                        SecurityUtils.ResetCertificate(_elements[i].Certificate);
                    }
                }
                SecurityUtils.ResetCertificate(_certificate);
            }
        }

        private IList<Claim> InitializeClaimsCore()
        {
            List<Claim> claims = new List<Claim>();
            byte[] thumbprint = _certificate.GetCertHash();
            claims.Add(new Claim(ClaimTypes.Thumbprint, thumbprint, Rights.Identity));
            claims.Add(new Claim(ClaimTypes.Thumbprint, thumbprint, Rights.PossessProperty));

            // Ordering SubjectName, Dns, SimpleName, Email, Upn
            string value = _certificate.SubjectName.Name;
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(Claim.CreateX500DistinguishedNameClaim(_certificate.SubjectName));
            }

            claims.AddRange(GetDnsClaims(_certificate));

            value = _certificate.GetNameInfo(X509NameType.SimpleName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(Claim.CreateNameClaim(value));
            }

            value = _certificate.GetNameInfo(X509NameType.EmailName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(Claim.CreateMailAddressClaim(new MailAddress(value)));
            }

            value = _certificate.GetNameInfo(X509NameType.UpnName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(Claim.CreateUpnClaim(value));
            }

            value = _certificate.GetNameInfo(X509NameType.UrlName, false);
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(Claim.CreateUriClaim(new Uri(value)));
            }

            if (_certificate.PublicKey.Key is RSA rsa)
            {
                claims.Add(Claim.CreateRsaClaim(rsa));
            }

            return claims;
        }

        private void EnsureClaims()
        {
            if (_claims != null)
            {
                return;
            }

            _claims = InitializeClaimsCore();
        }

        private static bool SupportedClaimType(string claimType)
        {
            return claimType == null ||
                ClaimTypes.Thumbprint.Equals(claimType) ||
                ClaimTypes.X500DistinguishedName.Equals(claimType) ||
                ClaimTypes.Dns.Equals(claimType) ||
                ClaimTypes.Name.Equals(claimType) ||
                ClaimTypes.Email.Equals(claimType) ||
                ClaimTypes.Upn.Equals(claimType) ||
                ClaimTypes.Uri.Equals(claimType) ||
                ClaimTypes.Rsa.Equals(claimType);
        }

        // Note: null string represents any.
        public override IEnumerable<Claim> FindClaims(string claimType, string right)
        {
            ThrowIfDisposed();
            if (!SupportedClaimType(claimType) || !SupportedRight(right))
            {
                yield break;
            }
            else if (_claims == null && ClaimTypes.Thumbprint.Equals(claimType))
            {
                if (right == null || Rights.Identity.Equals(right))
                {
                    yield return new Claim(ClaimTypes.Thumbprint, _certificate.GetCertHash(), Rights.Identity);
                }
                if (right == null || Rights.PossessProperty.Equals(right))
                {
                    yield return new Claim(ClaimTypes.Thumbprint, _certificate.GetCertHash(), Rights.PossessProperty);
                }
            }
            else if (_claims == null && ClaimTypes.Dns.Equals(claimType))
            {
                if (right == null || Rights.PossessProperty.Equals(right))
                {
                    foreach (Claim claim in GetDnsClaims(_certificate))
                    {
                        yield return claim;
                    }
                }
            }
            else
            {
                EnsureClaims();

                bool anyClaimType = (claimType == null);
                bool anyRight = (right == null);

                for (int i = 0; i < _claims.Count; ++i)
                {
                    Claim claim = _claims[i];
                    if ((claim != null) &&
                        (anyClaimType || claimType.Equals(claim.ClaimType)) &&
                        (anyRight || right.Equals(claim.Right)))
                    {
                        yield return claim;
                    }
                }
            }
        }

        private static List<Claim> GetDnsClaims(X509Certificate2 cert)
        {
            List<Claim> dnsClaimEntries = new List<Claim>();

            // old behavior, default for <= 4.6
            string value = cert.GetNameInfo(X509NameType.DnsName, false);
            if (!string.IsNullOrEmpty(value))
            {
                dnsClaimEntries.Add(Claim.CreateDnsClaim(value));
            }

            return dnsClaimEntries;
        }

        public override IEnumerator<Claim> GetEnumerator()
        {
            ThrowIfDisposed();
            EnsureClaims();
            return _claims.GetEnumerator();
        }

        public override string ToString()
        {
            return _disposed ? base.ToString() : SecurityUtils.ClaimSetToString(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }

        private class X500DistinguishedNameClaimSet : DefaultClaimSet, IIdentityInfo
        {
            public X500DistinguishedNameClaimSet(X500DistinguishedName x500DistinguishedName)
            {
                if (x500DistinguishedName == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(x500DistinguishedName));
                }

                Identity = new X509Identity(x500DistinguishedName);
                List<Claim> claims = new List<Claim>(2)
                {
                    new Claim(ClaimTypes.X500DistinguishedName, x500DistinguishedName, Rights.Identity),
                    Claim.CreateX500DistinguishedNameClaim(x500DistinguishedName)
                };
                Initialize(Anonymous, claims);
            }

            public IIdentity Identity { get; }
        }

        // We don't have a strongly typed extension to parse Subject Alt Names, so we have to do a workaround 
        // to figure out what the identifier, delimiter, and separator is by using a well-known extension
        private static class X509SubjectAlternativeNameConstants
        {
            public const string SanOid = "2.5.29.7";
            public const string San2Oid = "2.5.29.17";

            public static string Identifier
            {
                get;
                private set;
            }

            public static char Delimiter
            {
                get;
                private set;
            }

            public static string Separator
            {
                get;
                private set;
            }

            public static string[] SeparatorArray
            {
                get;
                private set;
            }

            public static bool SuccessfullyInitialized
            {
                get;
                private set;
            }


            // static initializer will run before properties are accessed
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Static constructors shouldn't throw so need to catch all exceptions")]
            static X509SubjectAlternativeNameConstants()
            {
                // Extracted a well-known X509Extension
                byte[] x509ExtensionBytes = new byte[] {
                    48, 36, 130, 21, 110, 111, 116, 45, 114, 101, 97, 108, 45, 115, 117, 98, 106, 101, 99,
                    116, 45, 110, 97, 109, 101, 130, 11, 101, 120, 97, 109, 112, 108, 101, 46, 99, 111, 109
                };
                const string subjectName = "not-real-subject-name";
                string x509ExtensionFormattedString = string.Empty;
                try
                {
                    X509Extension x509Extension = new X509Extension(SanOid, x509ExtensionBytes, true);
                    x509ExtensionFormattedString = x509Extension.Format(false);

                    // Each OS has a different dNSName identifier and delimiter
                    // On Windows, dNSName == "DNS Name" (localizable), on Linux, dNSName == "DNS"
                    // e.g.,
                    // Windows: x509ExtensionFormattedString is: "DNS Name=not-real-subject-name, DNS Name=example.com"
                    // Linux:   x509ExtensionFormattedString is: "DNS:not-real-subject-name, DNS:example.com"
                    // Parse: <identifier><delimiter><value><separator(s)>

                    int delimiterIndex = x509ExtensionFormattedString.IndexOf(subjectName) - 1;
                    Delimiter = x509ExtensionFormattedString[delimiterIndex];

                    // Make an assumption that all characters from the the start of string to the delimiter 
                    // are part of the identifier
                    Identifier = x509ExtensionFormattedString.Substring(0, delimiterIndex);

                    int separatorFirstChar = delimiterIndex + subjectName.Length + 1;
                    int separatorLength = 1;
                    for (int i = separatorFirstChar + 1; i < x509ExtensionFormattedString.Length; i++)
                    {
                        // We advance until the first character of the identifier to determine what the
                        // separator is. This assumes that the identifier assumption above is correct
                        if (x509ExtensionFormattedString[i] == Identifier[0])
                        {
                            break;
                        }

                        separatorLength++;
                    }

                    Separator = x509ExtensionFormattedString.Substring(separatorFirstChar, separatorLength);
                    SeparatorArray = new string[1] { Separator };
                    SuccessfullyInitialized = true;
                }
                catch (Exception ex)
                {
                    SuccessfullyInitialized = false;
                    DiagnosticUtility.TraceHandledException(
                        new FormatException(string.Format(CultureInfo.InvariantCulture,
                        "There was an error parsing the SubjectAlternativeNames: '{0}'. See inner exception for more details.{1}Detected values were: Identifier: '{2}'; Delimiter:'{3}'; Separator:'{4}'",
                        x509ExtensionFormattedString,
                        Environment.NewLine,
                        Identifier,
                        Delimiter,
                        Separator),
                        ex),
                        TraceEventType.Warning);
                }
            }
        }
    }

    internal class X509Identity : GenericIdentity, IDisposable
    {
        private const string X509 = "X509";
        private const string Thumbprint = "; ";
        private readonly X500DistinguishedName _x500DistinguishedName;
        private readonly X509Certificate2 _certificate;
        private string _name;
        private bool _disposed = false;
        private readonly bool _disposable = true;

        public X509Identity(X509Certificate2 certificate)
            : this(certificate, true, true)
        {
        }

        public X509Identity(X500DistinguishedName x500DistinguishedName)
            : base(X509, X509)
        {
            _x500DistinguishedName = x500DistinguishedName;
        }

        internal X509Identity(X509Certificate2 certificate, bool clone, bool disposable)
            : base(X509, X509)
        {
            _certificate = clone ? new X509Certificate2(certificate) : certificate;
            _disposable = clone || disposable;
        }

        public override string Name
        {
            get
            {
                ThrowIfDisposed();
                if (_name == null)
                {
                    //
                    // DCR 48092: PrincipalPermission authorization using certificates could cause Elevation of Privilege.
                    // because there could be duplicate subject name.  In order to be more unique, we use SubjectName + Thumbprint
                    // instead
                    //
                    _name = GetName() + Thumbprint + _certificate.Thumbprint;
                }
                return _name;
            }
        }

        private string GetName()
        {
            if (_x500DistinguishedName != null)
            {
                return _x500DistinguishedName.Name;
            }

            string value = _certificate.SubjectName.Name;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = _certificate.GetNameInfo(X509NameType.DnsName, false);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = _certificate.GetNameInfo(X509NameType.SimpleName, false);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = _certificate.GetNameInfo(X509NameType.EmailName, false);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = _certificate.GetNameInfo(X509NameType.UpnName, false);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return string.Empty;
        }

        public override ClaimsIdentity Clone()
        {
            return _certificate != null ? new X509Identity(_certificate) : new X509Identity(_x500DistinguishedName);
        }

        public void Dispose()
        {
            if (_disposable && !_disposed)
            {
                _disposed = true;
                if (_certificate != null)
                {
                    _certificate.Reset();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }
}
