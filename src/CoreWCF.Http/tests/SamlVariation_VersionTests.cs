// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using Xunit;

namespace SamlVariation
{
    /// <summary>
    /// Tests for SAML version-related variations and namespace confusion.  The default builder
    /// produces a SAML 1.1 assertion (Major=1, Minor=1) in the
    /// urn:oasis:names:tc:SAML:1.0:assertion namespace, which is what CoreWCF's
    /// SamlSecurityTokenHandler accepts.
    /// </summary>
    [Collection(SamlVariationCollection.Name)]
    public class SamlVersionTests
    {
        private readonly SamlVariationHostFixture _fx;

        public SamlVersionTests(SamlVariationHostFixture fx) { _fx = fx; }

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

        // Mismatched MajorVersion — SAML 1.1 wire format requires Major=1, Minor=1.
        [Fact]
        public void MajorVersionTwo_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlElement root = doc.DocumentElement;
                root.SetAttribute("MajorVersion", "2");
            };
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Mismatched MinorVersion — should be 1; flipping to 0 must be rejected.
        [Fact]
        public void MinorVersionZero_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlElement root = doc.DocumentElement;
                root.SetAttribute("MinorVersion", "0");
            };
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Wrong root namespace.  Replace urn:oasis:names:tc:SAML:1.0:assertion with the SAML 2
        // namespace at the root only.  Will fail to parse.
        [Fact]
        public void WrongRootNamespace_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            string xml = b.Build();
            string tampered = xml.Replace(
                "urn:oasis:names:tc:SAML:1.0:assertion",
                "urn:oasis:names:tc:SAML:2.0:assertion");
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, tampered, "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }
    }

    /// <summary>
     /// SAML 2.0 end-to-end tests live in <see cref="Saml2VariationTests"/> in
     /// SamlVariation_Saml2.cs.  That fixture configures a dedicated host whose binding
     /// advertises the SAML 2.0 token profile and whose Identity configuration drives the
     /// CoreWCF Saml2SecurityTokenHandler.
     /// </summary>
}
