// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace SamlVariation
{
    [Collection(SamlVariationCollection.Name)]
    public class SamlTimeConditionTests
    {
        private readonly SamlVariationHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public SamlTimeConditionTests(SamlVariationHostFixture fx, ITestOutputHelper output)
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
        public void WithinValidityWindow_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            string echoed = SamlEchoClient.InvokeEcho(_fx, Baseline().Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        [Fact]
        public void NotBefore_FarInFuture_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.NotBefore = DateTime.UtcNow.AddMinutes(30);
            b.NotOnOrAfter = DateTime.UtcNow.AddHours(2);

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NotOnOrAfter_FarInPast_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.NotBefore = DateTime.UtcNow.AddHours(-2);
            b.NotOnOrAfter = DateTime.UtcNow.AddMinutes(-30);

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // Within the default Microsoft.IdentityModel clock skew tolerance (5 min), a slightly
        // future NotBefore should still be accepted.
        [Fact]
        public void NotBefore_OneMinuteInFuture_WithinSkew_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.NotBefore = DateTime.UtcNow.AddMinutes(1);
            b.NotOnOrAfter = DateTime.UtcNow.AddHours(1);
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        [Fact]
        public void NotOnOrAfter_OneMinuteInPast_WithinSkew_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.NotBefore = DateTime.UtcNow.AddHours(-1);
            b.NotOnOrAfter = DateTime.UtcNow.AddMinutes(-1);
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }

        // Issued long ago but still within validity window should be accepted (issuance time
        // does not constrain validity beyond NotBefore/NotOnOrAfter).
        [Fact]
        public void IssueInstantInDistantPast_ButValid_IsAccepted()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.IssueInstant = DateTime.UtcNow.AddDays(-30);
            b.NotBefore = DateTime.UtcNow.AddMinutes(-1);
            b.NotOnOrAfter = DateTime.UtcNow.AddHours(1);
            string echoed = SamlEchoClient.InvokeEcho(_fx, b.Build(), "ok");
            Assert.Equal("ok", echoed);
        }
    }
}
