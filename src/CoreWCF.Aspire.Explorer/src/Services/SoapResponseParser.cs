// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Linq;

namespace CoreWCF.Aspire.Explorer.Services;

/// <summary>A single row of the formatted response grid.</summary>
public sealed record SoapResponseRow(string Name, string Value);

/// <summary>A parsed SOAP response: either result rows or a fault.</summary>
/// <param name="IsFault">Whether the service answered with a SOAP fault.</param>
/// <param name="FaultText">The fault reason, when it is a fault.</param>
/// <param name="Rows">Result rows, when it is not.</param>
/// <param name="FaultCode">The fault code, for example <c>Client</c> or <c>Sender</c>.</param>
public sealed record ParsedSoapResponse(
    bool IsFault,
    string? FaultText,
    IReadOnlyList<SoapResponseRow> Rows,
    string? FaultCode = null);

/// <summary>
/// Flattens a SOAP response body into Name / Value rows for the "Formatted" response view, and
/// surfaces SOAP faults. Mirrors the response grid of the classic WCF Test Client.
/// <para>
/// Faults are read with <see cref="MessageFault"/> rather than by looking for well-known element
/// names. SOAP 1.1 and 1.2 spell a fault completely differently - <c>faultstring</c>/<c>faultcode</c>
/// against <c>Reason</c>/<c>Code</c> - and WCF already knows both.
/// </para>
/// </summary>
public static class SoapResponseParser
{
    private static readonly IReadOnlyList<SoapResponseRow> s_noRows = Array.Empty<SoapResponseRow>();

    public static ParsedSoapResponse Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return new ParsedSoapResponse(false, null, s_noRows);
        }

        try
        {
            var version = MessageVersion.CreateVersion(DetectEnvelopeVersion(xml), AddressingVersion.None);

            using var reader = XmlReader.Create(new StringReader(xml));
            using var message = Message.CreateMessage(reader, SoapEnvelope.MaxBufferSize, version);

            // Buffered because a Message is read-once: inspecting IsFault or IsEmpty consumes it, and
            // the body reader afterwards would throw. Every step below takes its own copy.
            using var buffer = message.CreateBufferedCopy(SoapEnvelope.MaxBufferSize);

            bool isFault;
            using (var probe = buffer.CreateMessage())
            {
                isFault = probe.IsFault;
            }

            return isFault ? ReadFault(buffer) : ReadBody(buffer);
        }
        catch (XmlException)
        {
            return new ParsedSoapResponse(false, null, s_noRows);
        }
        catch (CommunicationException)
        {
            // Not a well-formed SOAP envelope; the raw text is still on the XML tab.
            return new ParsedSoapResponse(false, null, s_noRows);
        }
        catch (InvalidOperationException)
        {
            return new ParsedSoapResponse(false, null, s_noRows);
        }
    }

    private static ParsedSoapResponse ReadFault(MessageBuffer buffer)
    {
        using var message = buffer.CreateMessage();
        var fault = MessageFault.CreateFault(message, SoapEnvelope.MaxBufferSize);

        string? reason = null;
        try
        {
            // Throws rather than returning null when the fault carries no reason at all, which is
            // malformed but not worth failing the whole parse over.
            reason = fault.Reason?.GetMatchingTranslation()?.Text;
        }
        catch (ArgumentException)
        {
        }

        return new ParsedSoapResponse(true, reason, s_noRows, fault.Code?.Name);
    }

    private static ParsedSoapResponse ReadBody(MessageBuffer buffer)
    {
        using (var probe = buffer.CreateMessage())
        {
            if (probe.IsEmpty)
            {
                return new ParsedSoapResponse(false, null, s_noRows);
            }
        }

        using var message = buffer.CreateMessage();
        using var bodyReader = message.GetReaderAtBodyContents();

        // Read through a subtree, and then drain. WCF checks that the body reader finished at
        // end-of-file when it is disposed, and XElement.Load on the reader directly stops short of
        // that and trips the check.
        XElement wrapper;
        using (var subtree = bodyReader.ReadSubtree())
        {
            wrapper = XElement.Load(subtree);
        }

        while (!bodyReader.EOF && bodyReader.Read())
        {
        }

        // The wrapper is the response element; its children are the individual return values. An
        // operation returning a single complex type has no children, so fall back to the wrapper.
        var rows = wrapper.Elements()
            .Select(child => new SoapResponseRow(
                child.Name.LocalName,
                child.HasElements ? child.ToString() : child.Value))
            .ToList();

        if (rows.Count == 0)
        {
            rows.Add(new SoapResponseRow(wrapper.Name.LocalName, wrapper.Value));
        }

        return new ParsedSoapResponse(false, null, rows);
    }

    /// <summary>
    /// Picks the envelope version from the document itself. The response is text at this point - the
    /// operation's declared version is not necessarily what a misbehaving service actually replied with.
    /// </summary>
    private static EnvelopeVersion DetectEnvelopeVersion(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml));
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                return reader.NamespaceURI == "http://www.w3.org/2003/05/soap-envelope"
                    ? EnvelopeVersion.Soap12
                    : EnvelopeVersion.Soap11;
            }
        }

        return EnvelopeVersion.Soap11;
    }
}
