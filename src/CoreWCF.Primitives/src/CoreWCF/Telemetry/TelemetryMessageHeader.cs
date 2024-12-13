// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Telemetry;


internal class TelemetryMessageHeader : MessageHeader
{
    private const string NAMESPACE = "https://www.w3.org/TR/trace-context/";
    private readonly string name;

    private TelemetryMessageHeader(string name, string value)
    {
        this.name = name;
        this.Value = value;
    }

    public override string Name => this.name;

    public string Value { get; }

    public override string Namespace => NAMESPACE;

    public static TelemetryMessageHeader CreateHeader(string name, string value)
    {
        return new TelemetryMessageHeader(name, value);
    }

    public static TelemetryMessageHeader? FindHeader(string name, MessageHeaders allHeaders)
    {
        try
        {
            var headerIndex = allHeaders.FindHeader(name, NAMESPACE);
            if (headerIndex < 0)
            {
                return null;
            }

            using var reader = allHeaders.GetReaderAtHeader(headerIndex);
            reader.Read();
            return new TelemetryMessageHeader(name, reader.ReadContentAsString());
        }
        catch (XmlException)
        {
            return null;
        }
    }

    protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
    {
        writer.WriteString(this.Value);
    }
}
