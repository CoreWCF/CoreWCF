// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using CoreWCF.Aspire.Explorer.Services;
using Xunit;

namespace CoreWCF.Aspire.Explorer.Tests;

/// <summary>
/// Response parsing, including both SOAP fault dialects. A fault is spelled completely differently
/// in 1.1 and 1.2 - <c>faultcode</c>/<c>faultstring</c> against <c>Code</c>/<c>Reason</c> - so both
/// are covered explicitly.
/// </summary>
public class SoapResponseParserTests
{
    private const string Soap11Fault = """
        <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
          <s:Body>
            <s:Fault>
              <faultcode>s:Client</faultcode>
              <faultstring xml:lang="en">The operation failed on purpose</faultstring>
            </s:Fault>
          </s:Body>
        </s:Envelope>
        """;

    private const string Soap12Fault = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <s:Fault>
              <s:Code><s:Value>s:Sender</s:Value></s:Code>
              <s:Reason><s:Text xml:lang="en">The operation failed on purpose</s:Text></s:Reason>
            </s:Fault>
          </s:Body>
        </s:Envelope>
        """;

    private const string Soap11Response = """
        <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
          <s:Body>
            <AddResponse xmlns="http://tempuri.org/">
              <AddResult>42</AddResult>
            </AddResponse>
          </s:Body>
        </s:Envelope>
        """;

    [Fact]
    public void Parse_Soap11Fault_ReadsReasonAndCode()
    {
        var parsed = SoapResponseParser.Parse(Soap11Fault);

        Assert.True(parsed.IsFault);
        Assert.Equal("The operation failed on purpose", parsed.FaultText);
        Assert.Equal("Client", parsed.FaultCode);
        Assert.Empty(parsed.Rows);
    }

    [Fact]
    public void Parse_Soap12Fault_ReadsReasonAndCode()
    {
        var parsed = SoapResponseParser.Parse(Soap12Fault);

        Assert.True(parsed.IsFault);
        Assert.Equal("The operation failed on purpose", parsed.FaultText);
        Assert.Equal("Sender", parsed.FaultCode);
        Assert.Empty(parsed.Rows);
    }

    [Fact]
    public void Parse_Response_FlattensBodyIntoRows()
    {
        var parsed = SoapResponseParser.Parse(Soap11Response);

        Assert.False(parsed.IsFault);
        Assert.Null(parsed.FaultText);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal("AddResult", row.Name);
        Assert.Equal("42", row.Value);
    }

    [Fact]
    public void Parse_ResponseWithNoChildren_FallsBackToTheWrapper()
    {
        var parsed = SoapResponseParser.Parse("""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body><Plain xmlns="urn:x">value</Plain></s:Body>
            </s:Envelope>
            """);

        var row = Assert.Single(parsed.Rows);
        Assert.Equal("Plain", row.Name);
        Assert.Equal("value", row.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not xml at all")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    public void Parse_UnusableContent_ReturnsNothingRatherThanThrowing(string content)
    {
        // The raw text is still shown on the XML tab, so degrading quietly is the right behaviour -
        // an infrastructure error page in place of a SOAP response must not break the view.
        var parsed = SoapResponseParser.Parse(content);

        Assert.False(parsed.IsFault);
        Assert.Empty(parsed.Rows);
    }
}
