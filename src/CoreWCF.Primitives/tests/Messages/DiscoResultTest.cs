// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.Primitives.Tests.Messages;

public class DiscoResultTest
{
    [Fact]
    public async Task WriteDiscoResulTest()
    {
        var wsdlAddress = "http://localhost:8080/wsdl";
        var docAddress = "http://localhost:8080/doc";
        var discoResult = new ServiceMetadataExtension.HttpGetImpl.DiscoResult(wsdlAddress,docAddress);

        var httpResponse = new MockHttpResponse();
        await discoResult.WriteResponseAsync(httpResponse);

        httpResponse.Body.Position = 0;

        using var reader = new StreamReader(httpResponse.Body);

        var result = reader.ReadToEnd();
        var expected = """
                       <?xml version="1.0" encoding="utf-8"?><discovery xmlns="http://schemas.xmlsoap.org/disco/"><contractRef ref="http://localhost:8080/wsdl" docRef="http://localhost:8080/doc" xmlns="http://schemas.xmlsoap.org/disco/scl/" /></discovery>
                       """;
        Assert.Equal(expected, result);
    }
}
