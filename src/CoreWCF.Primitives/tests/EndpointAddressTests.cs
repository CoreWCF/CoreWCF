// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class EndpointAddressTests
    {
        [Theory]
        [MemberData(nameof(GetAddressingVersions))]
        public void WriteContentsToTest(AddressingVersion addressingVersion, string expectedOutput)
        {
            var serviceEndpointAddress = new EndpointAddress("http://example.org/service.svc");
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };
            StringBuilder sb = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(sb, settings))
            {
                serviceEndpointAddress.WriteContentsTo(addressingVersion, xmlWriter);
            }

            Assert.Equal(expectedOutput, sb.ToString());
        }

        [Theory]
        [MemberData(nameof(GetAddressingVersionsAnonymous))]
        public void WriteContentsToAnonymousUriTest(AddressingVersion addressingVersion, string expectedOutput)
        {
            var serviceEndpointAddress = new EndpointAddress(EndpointAddress.AnonymousUri);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };
            StringBuilder sb = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(sb, settings))
            {
                serviceEndpointAddress.WriteContentsTo(addressingVersion, xmlWriter);
            }

            Assert.Equal(expectedOutput, sb.ToString());
        }

        [Theory]
        [MemberData(nameof(GetAddressingVersionsNone))]
        public void WriteContentsToNoneUriTest(AddressingVersion addressingVersion, string expectedOutput)
        {
            var serviceEndpointAddress = new EndpointAddress(EndpointAddress.NoneUri);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };
            StringBuilder sb = new StringBuilder();

            var addressWritingDelegate = () =>
            {
                using (var xmlWriter = XmlWriter.Create(sb, settings))
                {
                    serviceEndpointAddress.WriteContentsTo(addressingVersion, xmlWriter);
                }
            };

            if (expectedOutput == null)
            {
                Assert.Throws<ArgumentException>(addressWritingDelegate);
            }
            else
            {
                addressWritingDelegate();
                Assert.Equal(expectedOutput, sb.ToString());
            }
        }

        public static IEnumerable<object []> GetAddressingVersions()
        {
            yield return new object[] { AddressingVersion.None, "http://example.org/service.svc" };
            yield return new object[] { AddressingVersion.WSAddressing10, @"<Address xmlns=""http://www.w3.org/2005/08/addressing"">http://example.org/service.svc</Address>" };
            yield return new object[] { AddressingVersion.WSAddressingAugust2004, @"<Address xmlns=""http://schemas.xmlsoap.org/ws/2004/08/addressing"">http://example.org/service.svc</Address>" };
        }

        public static IEnumerable<object[]> GetAddressingVersionsAnonymous()
        {
            yield return new object[] { AddressingVersion.None, EndpointAddress.AnonymousUri.AbsoluteUri };
            yield return new object[] { AddressingVersion.WSAddressing10, @"<Address xmlns=""http://www.w3.org/2005/08/addressing"">http://www.w3.org/2005/08/addressing/anonymous</Address>" };
            yield return new object[] { AddressingVersion.WSAddressingAugust2004, @"<Address xmlns=""http://schemas.xmlsoap.org/ws/2004/08/addressing"">http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</Address>" };
        }

        public static IEnumerable<object[]> GetAddressingVersionsNone()
        {
            yield return new object[] { AddressingVersion.None, EndpointAddress.NoneUri.AbsoluteUri };
            yield return new object[] { AddressingVersion.WSAddressing10, @"<Address xmlns=""http://www.w3.org/2005/08/addressing"">http://www.w3.org/2005/08/addressing/none</Address>" };
            yield return new object[] { AddressingVersion.WSAddressingAugust2004, null };
        }

    }
}
