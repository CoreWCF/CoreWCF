// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Security;
using Xunit;

namespace CoreWCF.Primitives.Tests.Security
{
    public class SignedXmlDocumentBuilderTests
    {
        private const string Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        private const string MarkerNs = "urn:test:marker";

        private const string EnvelopeWithSiblingHeader =
            @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope""
                         xmlns:wsse=""" + Wsse + @"""
                         xmlns:wsu=""" + Wsu + @"""
                         xmlns:m=""" + MarkerNs + @""">
              <s:Header>
                <m:Marker>
                  <wsu:Timestamp wsu:Id=""_smuggled"">
                    <wsu:Created>2026-01-01T00:00:00Z</wsu:Created>
                    <wsu:Expires>2026-01-01T00:05:00Z</wsu:Expires>
                  </wsu:Timestamp>
                </m:Marker>
                <wsse:Security s:mustUnderstand=""1"">
                  <wsu:Timestamp wsu:Id=""_current"">
                    <wsu:Created>2026-04-29T00:00:00Z</wsu:Created>
                    <wsu:Expires>2026-04-29T00:05:00Z</wsu:Expires>
                  </wsu:Timestamp>
                </wsse:Security>
              </s:Header>
              <s:Body><Ping xmlns=""urn:test""/></s:Body>
            </s:Envelope>";

        private const string Ds = "http://www.w3.org/2000/09/xmldsig#";

        private const string EnvelopeWithSignatureInSiblingHeader =
            @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope""
                         xmlns:wsse=""" + Wsse + @"""
                         xmlns:wsu=""" + Wsu + @"""
                         xmlns:m=""" + MarkerNs + @"""
                         xmlns:ds=""" + Ds + @""">
              <s:Header>
                <m:Marker>
                  <ds:Signature Id=""smuggled"">
                    <ds:SignedInfo>
                      <ds:CanonicalizationMethod Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#""/>
                      <ds:SignatureMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#rsa-sha1""/>
                      <ds:Reference URI=""#attacker-target"">
                        <ds:DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1""/>
                        <ds:DigestValue>AAAA</ds:DigestValue>
                      </ds:Reference>
                    </ds:SignedInfo>
                    <ds:SignatureValue>AAAA</ds:SignatureValue>
                  </ds:Signature>
                </m:Marker>
                <wsse:Security s:mustUnderstand=""1"">
                  <ds:Signature Id=""legitimate"">
                    <ds:SignedInfo>
                      <ds:CanonicalizationMethod Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#""/>
                      <ds:SignatureMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#rsa-sha1""/>
                      <ds:Reference URI=""#legitimate-target"">
                        <ds:DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1""/>
                        <ds:DigestValue>AAAA</ds:DigestValue>
                      </ds:Reference>
                    </ds:SignedInfo>
                    <ds:SignatureValue>AAAA</ds:SignatureValue>
                  </ds:Signature>
                </wsse:Security>
              </s:Header>
              <s:Body><Ping xmlns=""urn:test""/></s:Body>
            </s:Envelope>";

        [Fact]
        public void BuildSignedXmlDocument_IncludesAllHeadersForReferenceResolution()
        {
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                new MemoryStream(Encoding.UTF8.GetBytes(EnvelopeWithSiblingHeader)),
                XmlDictionaryReaderQuotas.Max);

            Message message = Message.CreateMessage(reader, int.MaxValue, MessageVersion.Soap12);
            int securityIndex = message.Headers.FindHeader("Security", Wsse);
            Assert.True(securityIndex >= 0, "Test setup failed: wsse:Security header not found.");

            XmlDocument doc = InvokeBuildSignedXmlDocument(message.Headers, securityIndex);

            // The verification document must include every header so that
            // signature Reference URIs targeting addressing headers
            // (wsa:To, wsa:Action, ...) and the Body still resolve. The
            // Signature-element lookup is constrained elsewhere (see
            // FindSecurityHeaderSignatureElement_IgnoresSignatureInSiblingHeader);
            // restricting the verification document itself is not what
            // protects against XML-signature substitution.
            XmlNode marker = doc.SelectSingleNode(
                "//*[local-name()='Marker' and namespace-uri()='" + MarkerNs + "']");
            Assert.NotNull(marker);

            XmlNode currentTimestamp = doc.SelectSingleNode(
                "//*[local-name()='Timestamp' and @*[local-name()='Id']='_current']");
            Assert.NotNull(currentTimestamp);
        }

        [Fact]
        public void FindSecurityHeaderSignatureElement_IgnoresSignatureInSiblingHeader()
        {
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                new MemoryStream(Encoding.UTF8.GetBytes(EnvelopeWithSignatureInSiblingHeader)),
                XmlDictionaryReaderQuotas.Max);

            Message message = Message.CreateMessage(reader, int.MaxValue, MessageVersion.Soap12);
            int securityIndex = message.Headers.FindHeader("Security", Wsse);
            Assert.True(securityIndex >= 0, "Test setup failed: wsse:Security header not found.");

            XmlDocument doc = InvokeBuildSignedXmlDocument(message.Headers, securityIndex);
            XmlElement element = InvokeFindSecurityHeaderSignatureElement(doc, securityIndex);

            // The Signature element selected for verification must be the
            // one inside wsse:Security ("legitimate"), not the one planted
            // in a sibling header that appears lexically earlier in the
            // envelope ("smuggled"). Selecting the smuggled signature
            // would let an unauthenticated attacker dictate which bytes
            // are cryptographically verified.
            string idAttr = element.GetAttribute("Id");
            Assert.Equal("legitimate", idAttr);
        }

        [Fact]
        public void BuildSignedXmlDocument_RejectsHeaderIndexThatIsNotSecurityHeader()
        {
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                new MemoryStream(Encoding.UTF8.GetBytes(EnvelopeWithSiblingHeader)),
                XmlDictionaryReaderQuotas.Max);

            Message message = Message.CreateMessage(reader, int.MaxValue, MessageVersion.Soap12);
            int markerIndex = message.Headers.FindHeader("Marker", MarkerNs);
            Assert.True(markerIndex >= 0, "Test setup failed: marker header not found.");

            // The helper must refuse to build a verification document over
            // any header other than the wsse:Security header itself.
            Assert.Throws<ArgumentException>(
                () => InvokeBuildSignedXmlDocument(message.Headers, markerIndex));
        }

        [Fact]
        public void FindSecurityHeaderSignatureElement_RejectsHeaderIndexThatIsNotSecurityHeader()
        {
            // Construct a verification-shaped document by hand whose child
            // at index 0 is a non-Security element. This exercises the
            // local defence-in-depth check inside
            // FindSecurityHeaderSignatureElement directly, independent of
            // any upstream validation performed by BuildSignedXmlDocument.
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("signed_xml_header");
            doc.AppendChild(root);
            XmlElement notSecurity = doc.CreateElement("m", "Marker", MarkerNs);
            XmlElement smuggledSignature = doc.CreateElement("ds", "Signature", Ds);
            notSecurity.AppendChild(smuggledSignature);
            root.AppendChild(notSecurity);

            Assert.Throws<MessageSecurityException>(
                () => InvokeFindSecurityHeaderSignatureElement(doc, 0));
        }

        private static XmlDocument InvokeBuildSignedXmlDocument(MessageHeaders headers, int headerIndex)
        {
            Assembly assembly = typeof(Message).Assembly;
            Type type = assembly.GetType("CoreWCF.Security.WSSecurityOneDotZeroReceiveSecurityHeader", throwOnError: true);
            MethodInfo method = type.GetMethod(
                "BuildSignedXmlDocument",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(method);

            try
            {
                return (XmlDocument)method.Invoke(null, new object[] { headers, headerIndex });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static XmlElement InvokeFindSecurityHeaderSignatureElement(XmlDocument doc, int headerIndex)
        {
            Assembly assembly = typeof(Message).Assembly;
            Type type = assembly.GetType("CoreWCF.Security.WSSecurityOneDotZeroReceiveSecurityHeader", throwOnError: true);
            MethodInfo method = type.GetMethod(
                "FindSecurityHeaderSignatureElement",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(method);

            try
            {
                return (XmlElement)method.Invoke(null, new object[] { doc, headerIndex });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}

