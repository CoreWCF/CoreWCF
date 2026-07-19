// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace SamlVariation
{
    [Collection(SamlVariationCollection.Name)]
    public class SamlIssuerVariationTests
    {
        private readonly SamlVariationHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public SamlIssuerVariationTests(SamlVariationHostFixture fx, ITestOutputHelper output)
        {
            _fx = fx;
            _output = output;
        }

        private SamlAssertionBuilder Baseline()
        {
            SamlAssertionBuilder b = new SamlAssertionBuilder
            {
                SigningCert = _fx.StsCert,
                KeyInfoCert = _fx.StsCert,
                KeyInfoStyle = KeyInfoStyle.X509Certificate,
            };
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });
            return b;
        }

        [Fact]
        public void RegisteredIssuer_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            string echoed = SamlEchoClient.InvokeEcho(_fx, Baseline().Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        // The Issuer attribute on the SAML 1.1 root assertion is informational; trust is
        // anchored on the X509 thumbprint registered in ConfigurationBasedIssuerNameRegistry.
        // An assertion claiming a different Issuer string but signed by the trusted STS cert
        // is still cryptographically authentic — the registry maps the cert thumbprint to a
        // canonical issuer name and the Issuer attribute is overwritten.
        [Fact]
        public void DifferentIssuerAttribute_ButSignedByTrustedCert_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.Issuer = "Some-Other-Issuer-Name";
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        // Signing certificate is not in the IssuerNameRegistry. Server must reject.
        [Fact]
        public void UnregisteredSigningCertificate_IsRejected()
        {
            using X509Certificate2 untrusted = SamlTestCryptography.CreateSelfSignedCert("CN=Unregistered-STS");
            using FirstChanceCapture cap = new FirstChanceCapture();

            SamlAssertionBuilder b = new SamlAssertionBuilder
            {
                SigningCert = untrusted,
                KeyInfoCert = untrusted,
                KeyInfoStyle = KeyInfoStyle.X509Certificate,
            };
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // Empty Issuer attribute. SAML 1.1 schema requires Issuer to be present and non-empty.
        // The SAML handler should refuse the assertion before signature validation.
        [Fact]
        public void EmptyIssuerString_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.Issuer = "";
            Exception caught = null;
            try
            {
                string xml = b.Build();
                SamlEchoClient.InvokeEcho(_fx, xml, "should-fail");
            }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }
    }
}
