// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SamlVariation
{
    [Collection(SamlVariationCollection.Name)]
    public class SamlAlgorithmVariationTests
    {
        private readonly SamlVariationHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public SamlAlgorithmVariationTests(SamlVariationHostFixture fx, ITestOutputHelper output)
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
        public void RsaSha256_Accepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.SignatureAlgorithm = SecurityAlgorithms.RsaSha256Signature;
            b.DigestAlgorithm = SecurityAlgorithms.Sha256Digest;
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        [Fact]
        public void RsaSha384_Accepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.SignatureAlgorithm = SecurityAlgorithms.RsaSha384Signature;
            b.DigestAlgorithm = SecurityAlgorithms.Sha384Digest;
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        [Fact]
        public void RsaSha512_Accepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.SignatureAlgorithm = SecurityAlgorithms.RsaSha512Signature;
            b.DigestAlgorithm = SecurityAlgorithms.Sha512Digest;
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        // RSA-SHA1 should be rejected by default by Microsoft.IdentityModel — the algorithm has
        // been forbidden since 6.x as a hardening default.
        [Fact]
        public void RsaSha1_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.SignatureAlgorithm = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
            b.DigestAlgorithm = "http://www.w3.org/2000/09/xmldsig#sha1";
            Exception caught = null;
            try
            {
                string xml = b.Build();
                SamlEchoClient.InvokeEcho(_fx, xml, "should-fail");
            }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Mismatched: digest method is SHA-1 but signature is SHA-256. This should be rejected
        // because SHA-1 is not in the default allowlist for digests.
        [Fact]
        public void Sha1Digest_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.SignatureAlgorithm = SecurityAlgorithms.RsaSha256Signature;
            b.DigestAlgorithm = "http://www.w3.org/2000/09/xmldsig#sha1";
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
