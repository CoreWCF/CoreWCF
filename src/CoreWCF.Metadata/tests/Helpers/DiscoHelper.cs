// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading.Tasks;
using System;
using AngleSharp.Diffing;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp;
using System.Collections.Generic;
using System.IO;
using Xunit;
using System.Xml.Linq;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace CoreWCF.Metadata.Tests.Helpers
{
    internal static class DiscoHelper
    {

        internal static async Task ValidateDiscoDocument(string metadataBaseAddress, string callerMethodName, string sourceFilePath, Action<HttpClient> configureHttpClient)
        {
            string generatedDoc = string.Empty;
            var discoAddress = metadataBaseAddress + "?disco";
            // As a new ASP.NET Core service is started for each test, there's no benefit from
            // cachine an HttpClient instance as a new port will be used and idle sockets will be closed.
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => true;
            using (var client = new HttpClient(httpClientHandler))
            {
                configureHttpClient?.Invoke(client);
                var response = await client.GetAsync(discoAddress);
                Assert.True(response.IsSuccessStatusCode, $"Response status for url {discoAddress} is {(int)response.StatusCode} {response.StatusCode} {response.ReasonPhrase}");
                Assert.Equal("text/xml; charset=UTF-8", response.Content.Headers.ContentType.ToString());
                generatedDoc = await response.Content.ReadAsStringAsync();
            }
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", generatedDoc);

            var contractRefUrl = metadataBaseAddress + "?wsdl";
            var docRefUrl = metadataBaseAddress;

            var expectedDiscoDoc = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <discovery xmlns="http://schemas.xmlsoap.org/disco/">
                        <contractRef ref="{contractRefUrl}" docRef="{docRefUrl}" xmlns="http://schemas.xmlsoap.org/disco/scl/"/>
                    </discovery>
                    """;

            // Canonicalize both disco documents so that comparison is semantic and not literal. This is because things like attributes in XML
            // are unordered unless canonicalized.
            generatedDoc = CanonicalizeXml(generatedDoc);
            expectedDiscoDoc = CanonicalizeXml(expectedDiscoDoc);
            Assert.Equal(expectedDiscoDoc, generatedDoc);
        }

        private static string CanonicalizeXml(string rawXmlTxt)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawXmlTxt);
            XmlDsigC14NTransform t = new XmlDsigC14NTransform();
            t.LoadInput(xmlDoc);
            Stream canonicalizedStream = (Stream)t.GetOutput();
            StreamReader reader = new StreamReader(canonicalizedStream);
            return reader.ReadToEnd();
        }
    }
}
