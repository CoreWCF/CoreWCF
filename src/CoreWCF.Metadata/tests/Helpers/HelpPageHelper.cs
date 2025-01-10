// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Diffing;
using AngleSharp.Diffing.Core;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Xunit;

namespace Helpers
{
    internal static class HelpPageHelper
    {
        internal static async Task ValidateHelpPage(string serviceMetadataPath, string callerMethodName, string sourceFilePath, Action<HttpClient> configureHttpClient = null)
        {
            string generatedHtml = string.Empty;
            // As a new ASP.NET Core service is started for each test, there's no benefit from
            // caching an HttpClient instance as a new port will be used and idle sockets will be closed.
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => true;
            using (var client = new HttpClient(httpClientHandler))
            {
                configureHttpClient?.Invoke(client);
                var response = await client.GetAsync(serviceMetadataPath);
                Assert.True(response.IsSuccessStatusCode, $"Response status for url {serviceMetadataPath} is {(int)response.StatusCode} {response.StatusCode} {response.ReasonPhrase}");
                // There's a bug in .NET 6 where it switches the charset to lower case. This is fixed in .NET 7 and later. Without ignoring the case
                // when doing the comparison, this test fails on .NET 6
                Assert.Equal("text/html; charset=UTF-8", response.Content.Headers.ContentType.ToString(), StringComparer.OrdinalIgnoreCase);
                generatedHtml = await response.Content.ReadAsStringAsync();
            }

            Assert.False(generatedHtml.StartsWith("<?xml"));
            var expectedHtmlFileName = Path.Combine("HelpPages", Path.GetFileNameWithoutExtension(sourceFilePath) + "." + callerMethodName + ".html");
            if (!File.Exists(expectedHtmlFileName))
            {
                // If sourceFilename.methodname.xml doesn't exist, then look for sourceFilename.html. This enables use of a single expected html file
                // for multiple tests in a single test class.
                var classHtmlFileName = Path.Combine("HelpPages", Path.GetFileNameWithoutExtension(sourceFilePath) + ".html");
                if (!File.Exists(classHtmlFileName))
                {
                    Assert.Fail($"Unable to find expected help page html file at {expectedHtmlFileName} or {classHtmlFileName}");
                }

                expectedHtmlFileName = classHtmlFileName;
            }

            var expectedHtml = File.ReadAllText(expectedHtmlFileName); // Net472 doesn't have an async variant of ReadAllText
            var diffs = DiffBuilder.Compare(expectedHtml)
                                   .WithTest(generatedHtml)
                                   .WithOptions(options =>
                                   {
                                       options.AddDefaultOptions()
                                              .AddFilter(DiscoAlternateLinkHRefFilter)
                                              .AddFilter(WsdlAnchorHRefFilter)
                                              .AddFilter(WsdlAnchorTextFilter);
                                   })
                                   .Build();

            // The three filters DiscoAlternateLinkHRefFilter, WsdlAnchorHRefFilter, and WsdlAnchorTextFilter are used to filter out
            // the expected differences in the generated html around the wsdl url being different and the anchor text being different.
            // This should result in diffs being empty.
            Assert.Empty(diffs);

            IConfiguration config = Configuration.Default;
            IBrowsingContext context = BrowsingContext.New(config);
            IHtmlParser parser = context.GetService<IHtmlParser>();
            IHtmlDocument document = parser.ParseDocument(generatedHtml);

            IHtmlLinkElement linkElement = document.QuerySelectorAll("link").Single() as IHtmlLinkElement;
            var linkHref = linkElement.GetAttribute("href");
            var expectedLinkHref = serviceMetadataPath + "?disco";
            Assert.Equal(expectedLinkHref, linkHref);

            IEnumerable<IHtmlAnchorElement> anchorElements = document.QuerySelectorAll("a").Cast<IHtmlAnchorElement>();
            var wsdlAnchor = anchorElements.Single(a => a.GetAttribute("href").EndsWith("?wsdl"));
            var expectedWsdlUrl = serviceMetadataPath + "?wsdl";
            Assert.Equal(expectedWsdlUrl, wsdlAnchor.GetAttribute("href"));
            Assert.Equal(expectedWsdlUrl, wsdlAnchor.Text.Trim());

            var singleWsdlAnchor = anchorElements.Single(a => a.GetAttribute("href").EndsWith("?singleWsdl"));
            var expectedSingleWsdlUrl = serviceMetadataPath + "?singleWsdl";
            Assert.Equal(expectedSingleWsdlUrl, singleWsdlAnchor.GetAttribute("href"));
            Assert.Equal(expectedSingleWsdlUrl, singleWsdlAnchor.Text.Trim());
        }

        internal static FilterDecision DiscoAlternateLinkHRefFilter(in AttributeComparisonSource source, FilterDecision currentDecision)
        {
            if (currentDecision.IsExclude())
            {
                return currentDecision;
            }

            if (source.Attribute.Name.Equals("href", StringComparison.OrdinalIgnoreCase))
            {
                if (source.Attribute.OwnerElement.NodeType == AngleSharp.Dom.NodeType.Element && source.Attribute.OwnerElement is IHtmlLinkElement)
                {
                    return FilterDecision.Exclude;
                }
            }

            return currentDecision;
        }

        internal static FilterDecision WsdlAnchorHRefFilter(in AttributeComparisonSource source, FilterDecision currentDecision)
        {
            if (currentDecision.IsExclude())
            {
                return currentDecision;
            }

            if (source.Attribute.Name.Equals("href", StringComparison.OrdinalIgnoreCase))
            {
                if (source.Attribute.OwnerElement.NodeType == AngleSharp.Dom.NodeType.Element && source.Attribute.OwnerElement is IHtmlAnchorElement)
                {
                    return FilterDecision.Exclude;
                }
            }

            return currentDecision;
        }

        internal static FilterDecision WsdlAnchorTextFilter(in ComparisonSource source, FilterDecision currentDecision)
        {
            if (currentDecision.IsExclude())
            {
                return currentDecision;
            }

            if (source.Node is IText textNode && textNode.ParentElement is IHtmlAnchorElement)
            {
                return FilterDecision.Exclude;
            }

            return currentDecision;
        }
    }
}
