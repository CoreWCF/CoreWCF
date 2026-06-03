// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace SamlVariation
{
    /// <summary>
    /// XML Signature Wrapping (XSW) and structural attacks against the SAML signature.  These
    /// are the classic "SAML signature bypass" CVE family — the signature is technically valid
    /// over its referenced element, but the receiver consumes a DIFFERENT element as the
    /// authoritative assertion content.
    /// </summary>
    [Collection(SamlVariationCollection.Name)]
    public class SamlStructuralAttackTests
    {
        private readonly SamlVariationHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public SamlStructuralAttackTests(SamlVariationHostFixture fx, ITestOutputHelper output)
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

        // After signing, mutate the assertion body without touching the Signature element.  Any
        // change should invalidate DigestValue and the signature must fail.
        [Fact]
        public void ContentTampered_AfterSigning_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlNamespaceManager ns = SamlAssertionBuilder.NsMgr(doc);
                XmlElement attrValue = (XmlElement)doc.SelectSingleNode(
                    "//saml:AttributeStatement/saml:Attribute/saml:AttributeValue", ns);
                if (attrValue != null) attrValue.InnerText = "elevated@example.com";
            };

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // Bit-flip the SignatureValue. Must be rejected.
        [Fact]
        public void TamperedSignatureValue_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlNamespaceManager ns = SamlAssertionBuilder.NsMgr(doc);
                XmlElement sigValue = (XmlElement)doc.SelectSingleNode("//ds:Signature/ds:SignatureValue", ns);
                if (sigValue == null) return;
                string b64 = sigValue.InnerText.Trim();
                byte[] bytes = Convert.FromBase64String(b64);
                bytes[0] ^= 0x01;
                sigValue.InnerText = Convert.ToBase64String(bytes);
            };

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
            Assert.Contains("security", SamlEchoClient.FlattenExceptions(caught), StringComparison.OrdinalIgnoreCase);
        }

        // XSW Variant 1: Wrap the signed assertion inside a new outer assertion that carries
        // attacker-controlled content.  The outer element is what the SAML reader will look at
        // first; the original signed element is preserved as a sibling/child so a naive
        // signature-only check still finds a valid signature somewhere in the document.
        [Fact]
        public void XsiWrapping_SiblingUnsignedAssertion_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder signed = Baseline();
            signed.SubjectName = "victim@example.com";
            string signedXml = signed.Build();

            // Build a second, attacker-controlled but unsigned, assertion with the SAME ID so a
            // "find element by AssertionID" lookup finds the wrong one.
            XmlDocument outerDoc = new XmlDocument { PreserveWhitespace = true };
            outerDoc.LoadXml(signedXml);
            string assertionId = outerDoc.DocumentElement.GetAttribute("AssertionID");

            SamlAssertionBuilder evil = Baseline();
            evil.IncludeSignature = false;
            evil.SubjectName = "attacker@example.com";
            evil.AssertionId = assertionId; // duplicate ID
            string evilXml = evil.Build();

            // Construct a wrapper that contains BOTH assertions and presents the malicious one
            // as the document root (consumed by the receiver).  Place the signed one inside an
            // <Extensions>-style wrapper so the signature can still be located.
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(evilXml); // root is now the attacker assertion
            XmlElement extensions = doc.CreateElement("Extensions", doc.DocumentElement.NamespaceURI);
            XmlDocument inner = new XmlDocument { PreserveWhitespace = true };
            inner.LoadXml(signedXml);
            XmlNode imported = doc.ImportNode(inner.DocumentElement, true);
            extensions.AppendChild(imported);
            doc.DocumentElement.AppendChild(extensions);

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, doc.OuterXml, "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // XSW Variant 2: Move the signed element into <ds:Signature><ds:Object> so a permissive
        // XML-DSig walker still validates the original element while the consumer reads the
        // outer (attacker-controlled) element.
        [Fact]
        public void XsiWrapping_SignedAssertionInsideObject_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder signed = Baseline();
            signed.SubjectName = "victim@example.com";
            string signedXml = signed.Build();

            XmlDocument signedDoc = new XmlDocument { PreserveWhitespace = true };
            signedDoc.LoadXml(signedXml);
            string assertionId = signedDoc.DocumentElement.GetAttribute("AssertionID");

            SamlAssertionBuilder evil = Baseline();
            evil.IncludeSignature = false;
            evil.SubjectName = "attacker@example.com";
            evil.AssertionId = assertionId;
            string evilXml = evil.Build();

            XmlDocument outerDoc = new XmlDocument { PreserveWhitespace = true };
            outerDoc.LoadXml(evilXml);

            // Locate the original Signature element, then wrap the original signed assertion as
            // a <ds:Object> inside that signature.
            XmlNamespaceManager ns = SamlAssertionBuilder.NsMgr(signedDoc);
            XmlElement sig = (XmlElement)signedDoc.SelectSingleNode("//ds:Signature", ns);
            if (sig == null) throw new InvalidOperationException("No signature on baseline.");
            // Append a Signature copy to the evil doc with the original assertion embedded as Object.
            XmlElement importedSig = (XmlElement)outerDoc.ImportNode(sig, true);
            XmlElement obj = outerDoc.CreateElement("ds", "Object", "http://www.w3.org/2000/09/xmldsig#");
            XmlElement importedAssertion = (XmlElement)outerDoc.ImportNode(signedDoc.DocumentElement, true);
            obj.AppendChild(importedAssertion);
            importedSig.AppendChild(obj);
            outerDoc.DocumentElement.AppendChild(importedSig);

            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, outerDoc.OuterXml, "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Mutate the signed Reference URI to point to a different element ID. Even if that
        // element exists with valid content, the digest in the original signature won't match.
        [Fact]
        public void TamperedReferenceUri_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlNamespaceManager ns = SamlAssertionBuilder.NsMgr(doc);
                XmlElement reference = (XmlElement)doc.SelectSingleNode("//ds:Signature/ds:SignedInfo/ds:Reference", ns);
                if (reference != null) reference.SetAttribute("URI", "#nonexistent-element");
            };
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Comment-injection in the URI reference: "#id<!--evil-->" technically refers to the
        // same element by ID but trips lenient XML parsers into different element resolution.
        [Fact]
        public void CommentInjectionInReferenceUri_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlNamespaceManager ns = SamlAssertionBuilder.NsMgr(doc);
                XmlElement reference = (XmlElement)doc.SelectSingleNode("//ds:Signature/ds:SignedInfo/ds:Reference", ns);
                if (reference == null) return;
                string original = reference.GetAttribute("URI");
                reference.SetAttribute("URI", original + "<!--xsw-->");
            };
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }

        // Add an extra unknown <ds:Transform> to the chain. SignedXml should refuse to process
        // an unknown transform.
        [Fact]
        public void UnknownTransformInChain_IsRejected()
        {
            using FirstChanceCapture cap = new FirstChanceCapture();
            SamlAssertionBuilder b = Baseline();
            b.PostSignMutate = doc =>
            {
                XmlNamespaceManager ns = SamlAssertionBuilder.NsMgr(doc);
                XmlElement transforms = (XmlElement)doc.SelectSingleNode(
                    "//ds:Signature/ds:SignedInfo/ds:Reference/ds:Transforms", ns);
                if (transforms == null) return;
                XmlElement bogus = doc.CreateElement("ds", "Transform", "http://www.w3.org/2000/09/xmldsig#");
                bogus.SetAttribute("Algorithm", "urn:test:made-up-transform");
                transforms.AppendChild(bogus);
            };
            Exception caught = null;
            try { SamlEchoClient.InvokeEcho(_fx, b.Build(), "should-fail"); }
            catch (Exception ex) { caught = ex; }
            Assert.NotNull(caught);
        }
    }
}
