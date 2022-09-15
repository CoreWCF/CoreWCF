// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Templates.Test.Helpers;

[DebuggerDisplay("{ToString(),nq}")]
public class AspNetProcess : IDisposable
{
    private const string ListeningMessagePrefix = "Now listening on: ";
    private readonly HttpClient _httpClient;
    private readonly ITestOutputHelper _output;
    private readonly DevelopmentCertificate _developmentCertificate;

    internal readonly Uri ListeningUri;
    internal ProcessEx Process { get; }

    public AspNetProcess(
        DevelopmentCertificate cert,
        ITestOutputHelper output,
        string workingDirectory,
        string dllPath,
        IDictionary<string, string> environmentVariables,
        bool published,
        bool hasListeningUri = true,
        bool usePublishedAppHost = false,
        ILogger logger = null)
    {
        _developmentCertificate = cert;
        _output = output;
        _httpClient = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => (certificate.Subject != "CN=localhost" && errors == SslPolicyErrors.None) || certificate?.Thumbprint == _developmentCertificate.CertificateThumbprint,
        })
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        output.WriteLine("Running ASP.NET Core application...");

        string process;
        string arguments;
        if (published)
        {
            if (usePublishedAppHost)
            {
                // When publishingu used the app host to run the app. This makes it easy to consistently run for regular and single-file publish
                process = Path.ChangeExtension(dllPath, OperatingSystem.IsWindows() ? ".exe" : null);
                arguments = null;
            }
            else
            {
                process = DotNetMuxer.MuxerPathOrDefault();
                arguments = $"exec {dllPath}";
            }
        }
        else
        {
            process = DotNetMuxer.MuxerPathOrDefault();

            // When executing "dotnet run", the launch urls specified in the app's launchSettings.json have higher precedence
            // than ambient environment variables. We specify the urls using command line arguments instead to allow us
            // to continue binding to "port 0" and avoid test flakiness due to port conflicts.
            arguments = $"run --no-build --urls \"{environmentVariables["ASPNETCORE_URLS"]}\"";
        }

        logger?.LogInformation($"AspNetProcess - process: {process} arguments: {arguments}");

        var finalEnvironmentVariables = new Dictionary<string, string>(environmentVariables)
        {
            ["ASPNETCORE_Kestrel__Certificates__Default__Path"] = _developmentCertificate.CertificatePath,
            ["ASPNETCORE_Kestrel__Certificates__Default__Password"] = _developmentCertificate.CertificatePassword,
        };

        Process = ProcessEx.Run(output, workingDirectory, process, arguments, envVars: finalEnvironmentVariables);

        logger?.LogInformation("AspNetProcess - process started");

        if (hasListeningUri)
        {
            logger?.LogInformation("AspNetProcess - Getting listening uri");
            ListeningUri = ResolveListeningUrl(output);
            logger?.LogInformation($"AspNetProcess - Got {ListeningUri}");
        }
    }

    public async Task AssertPagesOk(IEnumerable<Page> pages)
    {
        foreach (var page in pages)
        {
            await AssertOk(page.Url);
            await ContainsLinks(page);
        }
    }

    public async Task ContainsLinks(Page page)
    {
        var response = await RetryHelper.RetryRequest(async () =>
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(ListeningUri, page.Url));
            return await _httpClient.SendAsync(request);
        }, logger: NullLogger.Instance);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parser = new HtmlParser();
        var html = await parser.ParseAsync(await response.Content.ReadAsStreamAsync());

        foreach (IHtmlLinkElement styleSheet in html.GetElementsByTagName("link"))
        {
            Assert.Equal("stylesheet", styleSheet.Relation);
            // Workaround for https://github.com/dotnet/aspnetcore/issues/31030#issuecomment-811334450
            // Cleans up incorrectly generated filename for scoped CSS files
            var styleSheetHref = styleSheet.Href.Replace("_", string.Empty).Replace("about://", string.Empty);
            await AssertOk(styleSheetHref);
        }
        foreach (var script in html.Scripts)
        {
            if (!string.IsNullOrEmpty(script.Source))
            {
                await AssertOk(script.Source);
            }
        }

        Assert.True(html.Links.Length == page.Links.Count(), $"Expected {page.Url} to have {page.Links.Count()} links but it had {html.Links.Length}");
        foreach ((var link, var expectedLink) in html.Links.Zip(page.Links, Tuple.Create))
        {
            IHtmlAnchorElement anchor = (IHtmlAnchorElement)link;
            if (string.Equals(anchor.Protocol, "about:"))
            {
                Assert.True(anchor.PathName.EndsWith(expectedLink, StringComparison.Ordinal), $"Expected next link on {page.Url} to be {expectedLink} but it was {anchor.PathName}: {html.Source.Text}");
                await AssertOk(anchor.PathName);
            }
            else
            {
                Assert.True(string.Equals(anchor.Href, expectedLink), $"Expected next link to be {expectedLink} but it was {anchor.Href}.");
                var result = await RetryHelper.RetryRequest(async () =>
                {
                    return await _httpClient.GetAsync(anchor.Href);
                }, logger: NullLogger.Instance);

                Assert.True(IsSuccessStatusCode(result), $"{anchor.Href} is a broken link!");
            }
        }
    }

    private Uri ResolveListeningUrl(ITestOutputHelper output)
    {
        // Wait until the app is accepting HTTP requests
        output.WriteLine("Waiting until ASP.NET application is accepting connections...");
        var listeningMessage = GetListeningMessage();

        if (!string.IsNullOrEmpty(listeningMessage))
        {
            listeningMessage = listeningMessage.Trim();
            // Verify we have a valid URL to make requests to
            var listeningUrlString = listeningMessage.Substring(listeningMessage.IndexOf(
                ListeningMessagePrefix, StringComparison.Ordinal) + ListeningMessagePrefix.Length);

            output.WriteLine($"Detected that ASP.NET application is accepting connections on: {listeningUrlString}");
            listeningUrlString = string.Concat(listeningUrlString.AsSpan(0, listeningUrlString.IndexOf(':')),
                "://localhost",
                listeningUrlString.AsSpan(listeningUrlString.LastIndexOf(':')));

            output.WriteLine("Sending requests to " + listeningUrlString);
            return new Uri(listeningUrlString, UriKind.Absolute);
        }
        else
        {
            return null;
        }
    }

    private string GetListeningMessage()
    {
        var buffer = new List<string>();
        try
        {
            foreach (var line in Process.OutputLinesAsEnumerable)
            {
                if (line != null)
                {
                    buffer.Add(line);
                    if (line.Trim().Contains(ListeningMessagePrefix, StringComparison.Ordinal))
                    {
                        return line;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        throw new InvalidOperationException(@$"Couldn't find listening url:
{string.Join(Environment.NewLine, buffer)}");
    }

    private static bool IsSuccessStatusCode(HttpResponseMessage response)
    {
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect;
    }

    public Task AssertOk(string requestUrl)
        => AssertStatusCode(requestUrl, HttpStatusCode.OK);

    public Task AssertNotFound(string requestUrl)
        => AssertStatusCode(requestUrl, HttpStatusCode.NotFound);

    internal Task<HttpResponseMessage> SendRequest(string path) =>
        RetryHelper.RetryRequest(() => _httpClient.GetAsync(new Uri(ListeningUri, path)), logger: NullLogger.Instance);

    internal Task<HttpResponseMessage> SendRequest(Func<HttpRequestMessage> requestFactory)
        => RetryHelper.RetryRequest(() => _httpClient.SendAsync(requestFactory()), logger: NullLogger.Instance);

    public async Task AssertStatusCode(string requestUrl, HttpStatusCode statusCode, string acceptContentType = null)
    {
        var response = await RetryHelper.RetryRequest(() =>
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(ListeningUri, requestUrl));

            if (!string.IsNullOrEmpty(acceptContentType))
            {
                request.Headers.Add("Accept", acceptContentType);
            }

            return _httpClient.SendAsync(request);
        }, logger: NullLogger.Instance);

        Assert.True(statusCode == response.StatusCode, $"Expected {requestUrl} to have status '{statusCode}' but it was '{response.StatusCode}'.");
    }

    public async Task AssertServiceEndpoint()
    {
        const string action = "http://tempuri.org/IService/GetData";
        var response = await RetryHelper.RetryRequest(() =>
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(ListeningUri, "/Service.svc"));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");
            const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:GetData>
         <tem:value>42</tem:value>
      </tem:GetData>
   </s:Body>
</s:Envelope>";

            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            return _httpClient.SendAsync(request);
        }, logger: NullLogger.Instance);

        Assert.True(response.IsSuccessStatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        _output.WriteLine(responseBody);

        const string expected = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                                "<s:Body>" +
                                "<GetDataResponse xmlns=\"http://tempuri.org/\">" +
                                "<GetDataResult>You entered: 42</GetDataResult>" +
                                "</GetDataResponse>" +
                                "</s:Body>" +
                                "</s:Envelope>";

        Assert.Equal(expected, responseBody);
    }

    public async Task AssertMetadataEndpoint()
    {
        var response = await RetryHelper.RetryRequest(() =>
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(ListeningUri, "/Service.svc"));

            return _httpClient.SendAsync(request);
        }, logger: NullLogger.Instance);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parser = new HtmlParser();
        var html = await parser.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Contains(html.Links, x => x.InnerHtml == $"{ListeningUri}Service.svc?wsdl");
        Assert.Contains(html.Links, x => x.InnerHtml == $"{ListeningUri}Service.svc?singleWsdl");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        Process.Dispose();
    }

    public override string ToString()
    {
        var result = "";
        result += Process != null ? "Active: " : "Inactive";
        if (Process != null)
        {
            if (!Process.HasExited)
            {
                result += $"(Listening on {ListeningUri.OriginalString}) PID: {Process.Id}";
            }
            else
            {
                result += "(Already finished)";
            }
        }

        return result;
    }
}
