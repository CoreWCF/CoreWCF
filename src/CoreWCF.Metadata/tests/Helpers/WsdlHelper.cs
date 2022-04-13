// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Metadata.Tests.Helpers
{
    internal static class WsdlHelper
    {
        public static async Task ValidateSingleWsdl(string serviceMetadataPath, string endpointAddress,
                string callerMethodName, string sourceFilePath)
        {
            var singleWsdlPath = serviceMetadataPath + "?singleWsdl";
            string generatedWsdlTxt = string.Empty;
            // As a new ASP.NET Core service is started for each test, there's no benefit from
            // cachine an HttpClient instance as idle sockets will be closed.
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => true;
            using (var client = new HttpClient(httpClientHandler))
            {
                var response = await client.GetAsync(singleWsdlPath);
                Assert.True(response.IsSuccessStatusCode, $"Response status for url {singleWsdlPath} is {(int)response.StatusCode} {response.StatusCode} {response.ReasonPhrase}");
                generatedWsdlTxt = await response.Content.ReadAsStringAsync();
            }

            var xmlFileName = Path.Combine("Wsdls", Path.GetFileNameWithoutExtension(sourceFilePath) + "." + callerMethodName + ".xml");
            if (!File.Exists(xmlFileName))
            {
                // If sourceFilename.methodname.xml doesn't exist, then look for sourceFilename.xml. This enables use of a single expected wsdl file
                // for multiple tests in a single test class.
                var classXmlFileName = Path.Combine("Wsdls", Path.GetFileNameWithoutExtension(sourceFilePath) + ".xml");
                if (!File.Exists(classXmlFileName))
                {
                    Assert.True(false, $"Unable to find expected wsdl file at {xmlFileName} or {classXmlFileName}");
                }

                xmlFileName = classXmlFileName;
            }

            var expectedWsdlTxt = File.ReadAllText(xmlFileName); // Net472 doesn't have an async variant of ReadAllText

            // Make sure wsdl:definitions/wsdl:service/wsdl:port/soap:address(location) is correct as a first validation step
            ValidateServiceAddress(generatedWsdlTxt, endpointAddress);
            // Modify expected wsdl file to use the passed in location
            expectedWsdlTxt = FixLocationAddress(expectedWsdlTxt, endpointAddress);
            // Canonicalize both WSDL's so that comparison is semantic and not literal. This is because things like attributes in XML
            // are unordered unless canonicalized.
            generatedWsdlTxt = CanonicalizeXml(generatedWsdlTxt);
            expectedWsdlTxt = CanonicalizeXml(expectedWsdlTxt);
            Assert.Equal(expectedWsdlTxt, generatedWsdlTxt);
        }

        internal static async Task ValidateSingleWsdl(Uri[] serviceBaseAddresses, IDictionary<string, Binding> bindingEndpointMap,
            string callerMethodName, string sourceFilePath)
        {
            var serviceBaseAddress = serviceBaseAddresses.Where(uri => uri.Scheme == Uri.UriSchemeHttp).Single();
            var singleWsdlUriBuilder = new UriBuilder(serviceBaseAddress);
            singleWsdlUriBuilder.Host = serviceBaseAddress.Host;
            singleWsdlUriBuilder.Port = serviceBaseAddress.Port;
            singleWsdlUriBuilder.Query = "singleWsdl";
            var singleWsdlPath = singleWsdlUriBuilder.ToString();
            string generatedWsdlTxt = string.Empty;
            // As a new ASP.NET Core service is started for each test, there's no benefit from
            // cachine an HttpClient instance as idle sockets will be closed.
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(singleWsdlPath);
                Assert.True(response.IsSuccessStatusCode, $"Response status for url {singleWsdlPath} is {(int)response.StatusCode} {response.StatusCode} {response.ReasonPhrase}");
                generatedWsdlTxt = await response.Content.ReadAsStringAsync();
            }

            var xmlFileName = Path.Combine("Wsdls", Path.GetFileNameWithoutExtension(sourceFilePath) + "." + callerMethodName + ".xml");
            if (!File.Exists(xmlFileName))
            {
                // If sourceFilename.methodname.xml doesn't exist, then look for sourceFilename.xml. This enables use of a single expected wsdl file
                // for multiple tests in a single test class.
                var classXmlFileName = Path.Combine("Wsdls", Path.GetFileNameWithoutExtension(sourceFilePath) + ".xml");
                if (!File.Exists(classXmlFileName))
                {
                    Assert.True(false, $"Unable to find expected wsdl file at {xmlFileName} or {classXmlFileName}");
                }

                xmlFileName = classXmlFileName;
            }

            var expectedWsdlTxt = File.ReadAllText(xmlFileName); // Net472 doesn't have an async variant of ReadAllText

            // Make sure wsdl:definitions/wsdl:service/wsdl:port/soap:address(location) is correct as a first validation step
            ValidateServiceAddresses(generatedWsdlTxt, serviceBaseAddresses, bindingEndpointMap);
            // Modify expected wsdl file to use the passed in location
            expectedWsdlTxt = FixLocationAddresses(expectedWsdlTxt, serviceBaseAddresses, bindingEndpointMap.Keys);
            // Canonicalize both WSDL's so that comparison is semantic and not literal. This is because things like attributes in XML
            // are unordered unless canonicalized.
            generatedWsdlTxt = CanonicalizeXml(generatedWsdlTxt);
            expectedWsdlTxt = CanonicalizeXml(expectedWsdlTxt);
            Assert.Equal(expectedWsdlTxt, generatedWsdlTxt);
        }

        private static void ValidateServiceAddresses(string generatedWsdl, Uri[] serviceBaseAddresses, IDictionary<string, Binding> bindingEndpointMap)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(generatedWsdl);
            XPathNavigator navigator = xmlDoc.CreateNavigator();
            XmlNamespaceManager manager = new XmlNamespaceManager(navigator.NameTable);
            // Add our own ns prefix soapns to map to the wsdl soap ns so that navigator query can resolve a qualified query
            manager.AddNamespace("soapns", "http://schemas.xmlsoap.org/wsdl/soap/"); // Soap1.1
            manager.AddNamespace("soap12ns", "http://schemas.xmlsoap.org/wsdl/soap12/"); // Soap 1.2 
            manager.AddNamespace("wsa10ns", "http://www.w3.org/2005/08/addressing"); // AddressingVersion.WSAddressing10
            bool isSoap12 = false;
            var navs = navigator.Select($"//soapns:address", manager);
            if (navs.Count == 0)
            {
                isSoap12 = true;
                navs = navigator.Select($"//soap12ns:address", manager);
            }

            foreach (XPathNavigator nav in navs)
            {
                Assert.True(nav.HasAttributes);
                Assert.True(nav.MoveToAttribute("location", ""));
                if (!Uri.TryCreate(nav.Value, UriKind.Absolute, out Uri servicePathUri))
                {
                    Assert.False(true, $"Unable to parse location uri {nav.Value} as Uri");
                }

                bool validServiceAddress = false;
                foreach(var baseAddress in serviceBaseAddresses)
                {
                    foreach(var relativePath in bindingEndpointMap.Keys)
                    {
                        var possibleUri = new Uri(baseAddress.ToString() + "/" + relativePath);
                        if (possibleUri.Equals(servicePathUri))
                        {
                            validServiceAddress = true;
                            break;
                        }
                    }
                }

                Assert.True(validServiceAddress, $"Service address {servicePathUri} is not valid");
            }
            if (isSoap12) // Need to also check EndpointReference/Address
            {
                navs = navigator.Select($"//wsa10ns:Address", manager);
                foreach (XPathNavigator nav in navs)
                {
                    if (!Uri.TryCreate(nav.Value, UriKind.Absolute, out Uri servicePathUri))
                    {
                        Assert.False(true, $"Unable to parse location uri {nav.Value} as Uri");
                    }

                    bool validServiceAddress = false;
                    foreach (var baseAddress in serviceBaseAddresses)
                    {
                        foreach (var relativePath in bindingEndpointMap.Keys)
                        {
                            var possibleUri = new Uri(baseAddress.ToString() + "/" + relativePath);
                            if (possibleUri.Equals(servicePathUri))
                            {
                                validServiceAddress = true;
                                break;
                            }
                        }
                    }

                    Assert.True(validServiceAddress, $"Service address {servicePathUri} is not valid");
                }
            }
        }

        // This method is here to shortcut debugging new tests which are failing because of an incorrect path
        private static void ValidateServiceAddress(string generatedWsdl, string servicePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(generatedWsdl);
            XPathNavigator navigator = xmlDoc.CreateNavigator();
            XmlNamespaceManager manager = new XmlNamespaceManager(navigator.NameTable);
            // Add our own ns prefix soapns to map to the wsdl soap ns so that navigator query can resolve a qualified query
            manager.AddNamespace("soapns", "http://schemas.xmlsoap.org/wsdl/soap/"); // Soap1.1
            manager.AddNamespace("soap12ns", "http://schemas.xmlsoap.org/wsdl/soap12/"); // Soap 1.2 
            manager.AddNamespace("wsa10ns", "http://www.w3.org/2005/08/addressing"); // AddressingVersion.WSAddressing10
            bool isSoap12 = false;
            var navs = navigator.Select($"//soapns:address", manager);
            if (navs.Count == 0)
            {
                isSoap12 = true;
                navs = navigator.Select($"//soap12ns:address", manager);
            }
            Assert.Single(navs);
            foreach (XPathNavigator nav in navs)
            {
                // Should only execute once because of the Single assert
                Assert.True(nav.HasAttributes);
                Assert.True(nav.MoveToAttribute("location", ""));
                Assert.Equal(servicePath, nav.Value);
            }
            if(isSoap12) // Need to also fix EndpointReference/Address
            {
                navs = navigator.Select($"//wsa10ns:Address", manager);
                Assert.Single(navs);
                foreach (XPathNavigator nav in navs)
                {
                    // Should only execute once because of the Single assert
                    Assert.Equal(servicePath, nav.Value);
                }
            }
        }

        // This method modifies the expected wsdl to use the specified service path. This enables a single xml file
        // to be used for mutliple test variations where the hostname or url is changing.
        private static string FixLocationAddress(string originalXml, string servicePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(originalXml);
            XPathNavigator navigator = xmlDoc.CreateNavigator();
            XmlNamespaceManager manager = new XmlNamespaceManager(navigator.NameTable);
            // Add our own ns prefix soapns to map to the wsdl soap ns so that navigator query can resolve a qualified query
            manager.AddNamespace("soapns", "http://schemas.xmlsoap.org/wsdl/soap/"); // Soap1.1
            manager.AddNamespace("soap12ns", "http://schemas.xmlsoap.org/wsdl/soap12/"); // Soap 1.2 
            manager.AddNamespace("wsa10ns", "http://www.w3.org/2005/08/addressing"); // AddressingVersion.WSAddressing10
            bool isSoap12 = false;
            var navs = navigator.Select($"//soapns:address", manager);
            if (navs.Count == 0)
            {
                isSoap12 = true;
                navs = navigator.Select($"//soap12ns:address", manager);
            }
            Assert.Single(navs);
            foreach (XPathNavigator nav in navs)
            {
                // Should only execute once because of the Single assert
                Assert.True(nav.HasAttributes);
                Assert.True(nav.MoveToAttribute("location", ""));
                nav.SetValue(servicePath);
            }
            if (isSoap12)
            {
                navs = navigator.Select($"//wsa10ns:Address", manager);
                Assert.Single(navs);
                foreach (XPathNavigator nav in navs)
                {
                    // Should only execute once because of the Single assert
                    nav.SetValue(servicePath);
                }
            }

            return xmlDoc.InnerXml;
        }

        // This method modifies the expected wsdl to use the specified service path. This enables a single xml file
        // to be used for mutliple test variations where the hostname or url is changing.
        private static string FixLocationAddresses(string originalXml, Uri[] serviceBaseAddresses, ICollection<string> paths)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(originalXml);
            XPathNavigator navigator = xmlDoc.CreateNavigator();
            XmlNamespaceManager manager = new XmlNamespaceManager(navigator.NameTable);
            // Add our own ns prefix soapns to map to the wsdl soap ns so that navigator query can resolve a qualified query
            manager.AddNamespace("soapns", "http://schemas.xmlsoap.org/wsdl/soap/"); // Soap1.1
            manager.AddNamespace("soap12ns", "http://schemas.xmlsoap.org/wsdl/soap12/"); // Soap 1.2 
            manager.AddNamespace("wsa10ns", "http://www.w3.org/2005/08/addressing"); // AddressingVersion.WSAddressing10
            bool isSoap12 = false;
            var navs = navigator.Select($"//soapns:address", manager);
            if (navs.Count == 0)
            {
                isSoap12 = true;
                navs = navigator.Select($"//soap12ns:address", manager);
            }
            foreach (XPathNavigator nav in navs)
            {
                Assert.True(nav.HasAttributes);
                Assert.True(nav.MoveToAttribute("location", ""));
                var existingAddress = new Uri(nav.Value);
                foreach (var path in paths)
                {
                    if (nav.Value.EndsWith(path))
                    {
                        foreach (var baseAddress in serviceBaseAddresses)
                        {
                            if (existingAddress.Scheme == baseAddress.Scheme)
                            {
                                nav.SetValue(baseAddress.ToString() + "/" + path);
                                break;
                            }
                        }

                        break;
                    }
                }
            }
            if (isSoap12)
            {
                navs = navigator.Select($"//wsa10ns:Address", manager);
                foreach (XPathNavigator nav in navs)
                {
                    foreach (var path in paths)
                    {
                        var existingAddress = new Uri(nav.Value);
                        if (nav.Value.EndsWith(path))
                        {
                            foreach (var baseAddress in serviceBaseAddresses)
                            {
                                if (existingAddress.Scheme == baseAddress.Scheme)
                                {
                                    nav.SetValue(baseAddress.ToString() + "/" + path);
                                    break;
                                }
                            }

                            break;
                        }
                    }
                }
            }

            return xmlDoc.InnerXml;
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
