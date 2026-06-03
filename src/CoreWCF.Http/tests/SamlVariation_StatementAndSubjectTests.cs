// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace SamlVariation
{
    [Collection(SamlVariationCollection.Name)]
    public class SamlStatementVariationTests
    {
        private readonly SamlVariationHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public SamlStatementVariationTests(SamlVariationHostFixture fx, ITestOutputHelper output)
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
        public void AttributeStatementOnly_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.Statements.Add("attribute");
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        [Fact]
        public void AuthnStatementOnly_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.Statements.Add("authn");
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        [Fact]
        public void AuthnAndAttributeStatement_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.Statements.Add("authn");
            b.Statements.Add("attribute");
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        [Fact]
        public void TwoAttributeStatements_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.Statements.Add("attribute");
            b.Statements.Add("attribute");
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }
    }

    [Collection(SamlVariationCollection.Name)]
    public class SamlSubjectVariationTests
    {
        private readonly SamlVariationHostFixture _fx;

        public SamlSubjectVariationTests(SamlVariationHostFixture fx) { _fx = fx; }

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

        // Various NameIdentifier Format URIs. All are well-known SAML formats; the server should
        // accept any of them as long as the signature and audience are valid. Format is metadata
        // about the value, not gating.
        [Theory]
        [InlineData("http://schemas.xmlsoap.org/claims/upn", "user@example.com")]
        [InlineData("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress", "user@example.com")]
        [InlineData("urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified", "anything")]
        [InlineData("urn:oasis:names:tc:SAML:1.1:nameid-format:X509SubjectName", "CN=user, O=Test")]
        [InlineData("urn:oasis:names:tc:SAML:1.1:nameid-format:WindowsDomainQualifiedName", @"CONTOSO\user")]
        public void SubjectNameFormat_Accepted(string format, string name)
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.SubjectNameFormat = format;
            b.SubjectName = name;
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        [Theory]
        [InlineData("urn:oasis:names:tc:SAML:1.0:cm:bearer")]
        [InlineData("urn:oasis:names:tc:SAML:1.0:cm:sender-vouches")]
        public void ConfirmationMethod_BearerOrSenderVouches_Accepted(string method)
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.ConfirmationMethods.Clear();
            b.ConfirmationMethods.Add(method);
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        // Multiple confirmation methods on the same Subject — at least one is bearer.
        [Fact]
        public void MultipleConfirmationMethods_IncludingBearer_Accepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.ConfirmationMethods.Clear();
            b.ConfirmationMethods.Add("urn:oasis:names:tc:SAML:1.0:cm:bearer");
            b.ConfirmationMethods.Add("urn:oasis:names:tc:SAML:1.0:cm:sender-vouches");
            Assert.Equal("ok", SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok"));
        }

        // Holder-of-key confirmation requires a proof key in SubjectConfirmationData. Without
        // that, the assertion is malformed for h-o-k semantics; server should refuse to issue
        // a useful identity for it because no proof of possession was demonstrated.
        [Fact]
        public void HolderOfKey_WithoutProofKey_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.ConfirmationMethods.Clear();
            b.ConfirmationMethods.Add("urn:oasis:names:tc:SAML:1.0:cm:holder-of-key");
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        [Fact]
        public void UnknownConfirmationMethod_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.ConfirmationMethods.Clear();
            b.ConfirmationMethods.Add("urn:test:made-up-method");
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }
    }
}
