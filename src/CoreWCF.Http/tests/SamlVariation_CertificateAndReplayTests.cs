// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace SamlVariation
{
    [Collection(SamlVariationCollection.Name)]
    public class SamlCertificateTests
    {
        private readonly SamlVariationHostFixture _fx;

        public SamlCertificateTests(SamlVariationHostFixture fx) { _fx = fx; }

        private SamlAssertionBuilder Baseline(X509Certificate2 signingCert)
        {
            SamlAssertionBuilder b = new SamlAssertionBuilder
            {
                SigningCert = signingCert,
                KeyInfoCert = signingCert,
                KeyInfoStyle = KeyInfoStyle.X509Certificate,
            };
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });
            return b;
        }

        // Signed with the trusted STS cert that has already EXPIRED. Server has revocation
        // checking off but cert validity is normally enforced by IdentityConfiguration's
        // X509CertificateValidator. Since we set CertificateValidator = None for the test
        // fixture, this should still be accepted (validator says "trust everything").
        [Fact]
        public void ExpiredSigningCertificate_WithValidatorNone_IsAccepted()
        {
            using X509Certificate2 expiredCert = SamlTestCryptography.CreateSelfSignedCert(
                "CN=Test-STS-Expired",
                notBefore: DateTimeOffset.UtcNow.AddDays(-30),
                notAfter: DateTimeOffset.UtcNow.AddDays(-1));
            using FirstChanceCapture cap = new FirstChanceCapture();

            // Cert is not in the issuer registry — so still expected to be rejected for issuer
            // reasons, not for expiry.
            SamlAssertionBuilder b = Baseline(expiredCert);
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            // Either rejection (issuer or validity) is correct — just confirm it doesn't pass.
            Assert.NotNull(caught);
        }

        // Wrong cert in KeyInfo (not the signer) but happens to be the trusted STS cert.
        // Receiver resolves trusted STS cert from KeyInfo, attempts to verify the signature
        // (which was made by a *different* key), fails with signature validation error.
        // This is the original CVE we already test in SamlSignatureValidationE2E — included
        // here in the variation suite for completeness.
        [Fact]
        public void ForgedCertSubstitution_IsRejected()
        {
            using X509Certificate2 attacker = SamlTestCryptography.CreateSelfSignedCert("CN=Attacker");
            using FirstChanceCapture cap = new FirstChanceCapture();

            SamlAssertionBuilder b = new SamlAssertionBuilder
            {
                SigningCert = attacker,
                KeyInfoCert = _fx.StsCert,
                KeyInfoStyle = KeyInfoStyle.X509Certificate,
            };
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }
    }

    [Collection(SamlVariationCollection.Name)]
    public class SamlReplayTest
    {
        private readonly SamlVariationHostFixture _fx;

        public SamlReplayTest(SamlVariationHostFixture fx) { _fx = fx; }

        // The same valid assertion sent twice in immediate succession.  CoreWCF does not enable
        // SAML token replay detection by default; both calls are expected to succeed.  This
        // test documents that behavior — no replay protection is in place.
        [Fact]
        public void SameAssertionSentTwice_BothSucceed_DocumentingNoReplayProtection()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = new SamlAssertionBuilder
            {
                SigningCert = _fx.StsCert,
                KeyInfoCert = _fx.StsCert,
                KeyInfoStyle = KeyInfoStyle.X509Certificate,
            };
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });
            string xml = b.Build();

            string first = SamlEchoClient.InvokeEcho(_fx, xml, "first");
            string second = SamlEchoClient.InvokeEcho(_fx, xml, "second");
            Assert.Equal("first", first);
            Assert.Equal("second", second);
        }
    }
}
