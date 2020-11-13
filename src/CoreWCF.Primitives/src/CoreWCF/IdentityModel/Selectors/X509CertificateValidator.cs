using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    public abstract class X509CertificateValidator
    {
        internal const uint CAPI_CERT_CHAIN_POLICY_NT_AUTH = 6;

        static X509CertificateValidator peerTrust;
        static X509CertificateValidator chainTrust;
        static X509CertificateValidator ntAuthChainTrust;
        static X509CertificateValidator peerOrChainTrust;
        static X509CertificateValidator none;

        public static X509CertificateValidator None
        {
            get
            {
                if (none == null)
                    none = new NoneX509CertificateValidator();
                return none;
            }
        }

        public static X509CertificateValidator PeerTrust
        {
            get
            {
                if (peerTrust == null)
                    peerTrust = new PeerTrustValidator();
                return peerTrust;
            }
        }

        public static X509CertificateValidator ChainTrust
        {
            get
            {
                if (chainTrust == null)
                    chainTrust = new ChainTrustValidator();
                return chainTrust;
            }
        }

        // TODO: Consider creating platform specific package which contains windows only implementations such as NTAuthChainTrust
        internal static X509CertificateValidator NTAuthChainTrust
        {
            get
            {
                if (ntAuthChainTrust == null)
                    ntAuthChainTrust = new ChainTrustValidator(false, null, CAPI_CERT_CHAIN_POLICY_NT_AUTH);
                return ntAuthChainTrust;
            }
        }

        public static X509CertificateValidator PeerOrChainTrust
        {
            get
            {
                if (peerOrChainTrust == null)
                    peerOrChainTrust = new PeerOrChainTrustValidator();
                return peerOrChainTrust;
            }
        }

        public static X509CertificateValidator CreateChainTrustValidator(bool useMachineContext, X509ChainPolicy chainPolicy)
        {
            if (chainPolicy == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(chainPolicy));
            return new ChainTrustValidator(useMachineContext, chainPolicy, X509CertificateChain.DefaultChainPolicyOID);
        }

        public static X509CertificateValidator CreatePeerOrChainTrustValidator(bool useMachineContext, X509ChainPolicy chainPolicy)
        {
            if (chainPolicy == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(chainPolicy));
            return new PeerOrChainTrustValidator(useMachineContext, chainPolicy);
        }

        public abstract void Validate(X509Certificate2 certificate);

        class NoneX509CertificateValidator : X509CertificateValidator
        {
            public override void Validate(X509Certificate2 certificate)
            {
                if (certificate == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }
        }

        class PeerTrustValidator : X509CertificateValidator
        {
            public override void Validate(X509Certificate2 certificate)
            {
                if (certificate == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));

                Exception exception;
                if (!TryValidate(certificate, out exception))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(exception);
            }

            static bool StoreContainsCertificate(StoreName storeName, X509Certificate2 certificate)
            {
                X509Store store = new X509Store(storeName, StoreLocation.CurrentUser);
                X509Certificate2Collection certificates = null;
                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                    return certificates.Count > 0;
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception :" + e.Message);
                    return false;
                }
                finally
                {
                    SecurityUtils.ResetAllCertificates(certificates);
                    store.Close();
                }
            }

            internal bool TryValidate(X509Certificate2 certificate, out Exception exception)
            {
                // Checklist
                // 1) time validity of cert
                // 2) in trusted people store
                // 3) not in disallowed store

                // The following code could be written as:
                // DateTime now = DateTime.UtcNow;
                // if (now > certificate.NotAfter.ToUniversalTime() || now < certificate.NotBefore.ToUniversalTime())
                //
                // this is because X509Certificate2.xxx doesn't return UT.  However this would be a SMALL perf hit.
                // I put a DebugAssert so that this will ensure that the we are compatible with the CLR we shipped with

                DateTime now = DateTime.Now;
                DiagnosticUtility.DebugAssert(now.Kind == certificate.NotAfter.Kind && now.Kind == certificate.NotBefore.Kind, "");

                if (now > certificate.NotAfter || now < certificate.NotBefore)
                {
                    exception = new SecurityTokenValidationException(SR.Format(SR.X509InvalidUsageTime,
                        SecurityUtils.GetCertificateId(certificate), now, certificate.NotBefore, certificate.NotAfter));
                    return false;
                }

                if (!StoreContainsCertificate(StoreName.TrustedPeople, certificate))
                {
                    exception = new SecurityTokenValidationException(SR.Format(SR.X509IsNotInTrustedStore,
                        SecurityUtils.GetCertificateId(certificate)));
                    return false;
                }

                if (StoreContainsCertificate(StoreName.Disallowed, certificate))
                {
                    exception = new SecurityTokenValidationException(SR.Format(SR.X509IsInUntrustedStore,
                        SecurityUtils.GetCertificateId(certificate)));
                    return false;
                }
                exception = null;
                return true;
            }
        }

        class ChainTrustValidator : X509CertificateValidator
        {
            bool useMachineContext;
            X509ChainPolicy chainPolicy;
            uint chainPolicyOID = X509CertificateChain.DefaultChainPolicyOID;

            public ChainTrustValidator()
            {
                chainPolicy = null;
            }

            public ChainTrustValidator(bool useMachineContext, X509ChainPolicy chainPolicy, uint chainPolicyOID)
            {
                this.useMachineContext = useMachineContext;
                this.chainPolicy = chainPolicy;
                this.chainPolicyOID = chainPolicyOID;
            }

            public override void Validate(X509Certificate2 certificate)
            {
                if (certificate == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));

                X509Chain chain = new X509Chain();
                if (chainPolicy != null)
                {
                    chain.ChainPolicy = chainPolicy;
                }

                if (!chain.Build(certificate))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenValidationException(SR.Format(SR.X509ChainBuildFail,
                        SecurityUtils.GetCertificateId(certificate), GetChainStatusInformation(chain.ChainStatus))));
                }
            }

            static string GetChainStatusInformation(X509ChainStatus[] chainStatus)
            {
                if (chainStatus != null)
                {
                    StringBuilder error = new StringBuilder(128);
                    for (int i = 0; i < chainStatus.Length; ++i)
                    {
                        error.Append(chainStatus[i].StatusInformation);
                        error.Append(" ");
                    }
                    return error.ToString();
                }
                return string.Empty;
            }
        }

        class PeerOrChainTrustValidator : X509CertificateValidator
        {
            X509CertificateValidator chain;
            PeerTrustValidator peer;

            public PeerOrChainTrustValidator()
            {
                chain = X509CertificateValidator.ChainTrust;
                peer = (PeerTrustValidator)X509CertificateValidator.PeerTrust;
            }

            public PeerOrChainTrustValidator(bool useMachineContext, X509ChainPolicy chainPolicy)
            {
                chain = X509CertificateValidator.CreateChainTrustValidator(useMachineContext, chainPolicy);
                peer = (PeerTrustValidator)X509CertificateValidator.PeerTrust;
            }

            public override void Validate(X509Certificate2 certificate)
            {
                if (certificate == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));

                Exception exception;
                if (peer.TryValidate(certificate, out exception))
                    return;

                try
                {
                    chain.Validate(certificate);
                }
                catch (SecurityTokenValidationException ex)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenValidationException(exception.Message + " " + ex.Message));
                }
            }
        }
    }

}
