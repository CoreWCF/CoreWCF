// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace SamlVariation
{
    [Collection(SamlVariationCollection.Name)]
    public class SamlAudienceVariationTests
    {
        private readonly SamlVariationHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public SamlAudienceVariationTests(SamlVariationHostFixture fx, ITestOutputHelper output)
        {
            _fx = fx;
            _output = output;
        }

        private SamlAssertionBuilder Baseline() => new SamlAssertionBuilder
        {
            SigningCert = _fx.StsCert,
            KeyInfoCert = _fx.StsCert,
            KeyInfoStyle = KeyInfoStyle.X509Certificate,
        };

        // Single AudienceRestriction with single matching Audience. Baseline positive.
        [Fact]
        public void SingleAudience_Match_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "hi");
            Assert.Equal("hi", echoed);
        }

        // Single AudienceRestriction containing the matching audience among several. The matcher
        // should treat values inside one block as OR — at least one must match.
        [Fact]
        public void MultipleAudienceUrisInOneRestriction_ContainingMatch_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string>
            {
                "https://other.example/api",
                _fx.ServiceAddress.AbsoluteUri,
                "https://yet-another.example/api",
            });
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        // Multiple AudienceRestriction blocks where every block contains a match (AND semantics).
        [Fact]
        public void MultipleRestrictionBlocks_AllContainMatch_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri, "https://other.example/api" });
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        // Multiple restriction blocks where one block does NOT contain the configured audience.
        // SAML AudienceRestriction semantics: every block must contain a match (AND). Therefore
        // the assertion must be rejected.
        [Fact]
        public void MultipleRestrictionBlocks_OneMissingMatch_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri });
            b.AudienceRestrictionBlocks.Add(new List<string> { "https://other.example/api" });

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoAudienceMatch_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string> { "https://wrong.example/api" });

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // No AudienceRestriction in the token at all when the server REQUIRES it
        // (AudienceUriMode = Always — the default). Server must reject.
        [Fact]
        public void NoAudienceRestriction_WhenRequired_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            // No restriction blocks added.
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // A prefix of the configured audience must NOT count as a match.
        // (Server is configured with https://localhost:PORT/SamlE2E.)
        [Fact]
        public void AudiencePrefix_DoesNotMatch_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            // Truncate the path — host+port match but path does not.
            string prefix = $"https://localhost:{_fx.ServiceAddress.Port}/Saml";
            b.AudienceRestrictionBlocks.Add(new List<string> { prefix });
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // A superstring of the configured audience must NOT match either.
        [Fact]
        public void AudienceSuperstring_DoesNotMatch_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri + "/extra" });
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Adding a trailing slash should NOT match an exact-URI configuration. This documents
        // the actual behavior — if it is accepted, the test will fail and the report lists it.
        [Fact]
        public void AudienceTrailingSlash_DoesNotMatch_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.AudienceRestrictionBlocks.Add(new List<string> { _fx.ServiceAddress.AbsoluteUri + "/" });
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Case sensitivity: per RFC 3986 the scheme/host portions of a URI are case-insensitive
        // but the path is case-sensitive. Test both: changing scheme/host case should still match;
        // changing path case should not.
        [Fact]
        public void AudienceCase_HostUppercased_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            string upperHost = _fx.ServiceAddress.AbsoluteUri.Replace("localhost", "LOCALHOST");
            b.AudienceRestrictionBlocks.Add(new List<string> { upperHost });
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        [Fact]
        public void AudienceCase_PathChanged_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            string upperPath = _fx.ServiceAddress.AbsoluteUri.Replace("/SamlE2E", "/SAMLE2E");
            b.AudienceRestrictionBlocks.Add(new List<string> { upperPath });
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }
    }
}
