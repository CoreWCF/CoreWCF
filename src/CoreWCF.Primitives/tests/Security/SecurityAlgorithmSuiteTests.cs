// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Xml;
using CoreWCF.Security;
using Xunit;

namespace CoreWCF.Primitives.Tests.Security
{
    public class SecurityAlgorithmSuiteTests
    {
        private const string Sha1DigestAlgorithm = "http://www.w3.org/2000/09/xmldsig#sha1";
        private const string Sha256DigestAlgorithm = "http://www.w3.org/2001/04/xmlenc#sha256";
        private const string RsaSha256SignatureAlgorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        private const string ExcC14NCanonicalizationAlgorithm = "http://www.w3.org/2001/10/xml-exc-c14n#";
        private const string DsNamespace = "http://www.w3.org/2000/09/xmldsig#";

        [Fact]
        public void Basic256Sha256_DeclaresSha256AsOnlySupportedDigestAlgorithm()
        {
            SecurityAlgorithmSuite suite = SecurityAlgorithmSuite.Basic256Sha256;

            Assert.Equal(Sha256DigestAlgorithm, suite.DefaultDigestAlgorithm);
            Assert.True(suite.IsDigestAlgorithmSupported(Sha256DigestAlgorithm));
            Assert.False(suite.IsDigestAlgorithmSupported(Sha1DigestAlgorithm));
        }

        [Fact]
        public void Basic256_DeclaresSha1AsSupportedDigestAlgorithm()
        {
            SecurityAlgorithmSuite suite = SecurityAlgorithmSuite.Basic256;

            Assert.Equal(Sha1DigestAlgorithm, suite.DefaultDigestAlgorithm);
            Assert.True(suite.IsDigestAlgorithmSupported(Sha1DigestAlgorithm));
            Assert.False(suite.IsDigestAlgorithmSupported(Sha256DigestAlgorithm));
        }

        [Fact]
        public void Basic256Sha256_RejectsReferenceDigestThatIsNotSha256()
        {
            SignedXml signedXml = LoadSignedXml(BuildSignatureXml(Sha1DigestAlgorithm));

            MessageSecurityException ex = Assert.Throws<MessageSecurityException>(
                () => InvokeEnforceReferenceDigestPolicy(signedXml, SecurityAlgorithmSuite.Basic256Sha256));

            Assert.Contains(Sha1DigestAlgorithm, ex.Message);
        }

        [Fact]
        public void Basic256Sha256_AcceptsReferenceDigestThatMatchesSuite()
        {
            SignedXml signedXml = LoadSignedXml(BuildSignatureXml(Sha256DigestAlgorithm));

            // Should complete without throwing when every Reference DigestMethod
            // matches the suite's DefaultDigestAlgorithm.
            InvokeEnforceReferenceDigestPolicy(signedXml, SecurityAlgorithmSuite.Basic256Sha256);
        }

        [Fact]
        public void Basic256_AcceptsSha1ReferenceDigest()
        {
            SignedXml signedXml = LoadSignedXml(BuildSignatureXml(Sha1DigestAlgorithm));

            // Basic256's DefaultDigestAlgorithm is SHA-1, so a SHA-1 reference
            // digest must remain accepted by this enforcement helper.
            InvokeEnforceReferenceDigestPolicy(signedXml, SecurityAlgorithmSuite.Basic256);
        }

        private static string BuildSignatureXml(string referenceDigestAlgorithm) =>
            $@"<Signature xmlns=""{DsNamespace}"">
                 <SignedInfo>
                   <CanonicalizationMethod Algorithm=""{ExcC14NCanonicalizationAlgorithm}"" />
                   <SignatureMethod Algorithm=""{RsaSha256SignatureAlgorithm}"" />
                   <Reference URI=""#_target"">
                     <Transforms>
                       <Transform Algorithm=""{ExcC14NCanonicalizationAlgorithm}"" />
                     </Transforms>
                     <DigestMethod Algorithm=""{referenceDigestAlgorithm}"" />
                     <DigestValue>AAAAAAAAAAAAAAAAAAAAAAAAAAA=</DigestValue>
                   </Reference>
                 </SignedInfo>
                 <SignatureValue>AAAA</SignatureValue>
               </Signature>";

        private static SignedXml LoadSignedXml(string signatureXml)
        {
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(signatureXml);
            SignedXml signedXml = new SignedXml(doc);
            signedXml.LoadXml(doc.DocumentElement);
            return signedXml;
        }

        private static void InvokeEnforceReferenceDigestPolicy(SignedXml signedXml, SecurityAlgorithmSuite suite)
        {
            Assembly assembly = typeof(SecurityAlgorithmSuite).Assembly;
            Type type = assembly.GetType(
                "CoreWCF.Security.WSSecurityOneDotZeroReceiveSecurityHeader",
                throwOnError: true);

            MethodInfo method = type.GetMethod(
                "EnforceReferenceDigestPolicy",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.NotNull(method);

            try
            {
                method.Invoke(null, new object[] { signedXml, suite });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}
