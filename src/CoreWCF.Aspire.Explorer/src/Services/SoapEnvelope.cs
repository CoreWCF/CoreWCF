// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CoreWCF.Aspire.Explorer.Model;

namespace CoreWCF.Aspire.Explorer.Services;

/// <summary>Builds SOAP envelopes around a body payload, using the WCF message stack.</summary>
public static class SoapEnvelope
{
    internal const int MaxBufferSize = 64 * 1024 * 1024;

    /// <summary>
    /// Wraps a body element in an envelope of the given SOAP version.
    /// <para>
    /// The envelope comes from <see cref="Message"/> rather than hand-assembled XML, so the sample
    /// shown in the editor is shaped by exactly the same code that will put it on the wire.
    /// </para>
    /// </summary>
    public static string Wrap(XElement? body, SoapVersion version)
    {
        var messageVersion = VersionFor(version);

        // A null action keeps WCF from adding an addressing Action header to the sample. The action
        // is applied by the invoker at send time, where it belongs.
        using var message = body is null
            ? Message.CreateMessage(messageVersion, action: null)
            : CreateMessage(messageVersion, body);

        return Write(message);
    }

    /// <summary>The WCF message version matching a WSDL binding's SOAP version, without addressing.</summary>
    internal static MessageVersion VersionFor(SoapVersion version) => MessageVersion.CreateVersion(
        version == SoapVersion.Soap12 ? EnvelopeVersion.Soap12 : EnvelopeVersion.Soap11,
        AddressingVersion.None);

    private static Message CreateMessage(MessageVersion messageVersion, XElement body)
    {
        using var reader = body.CreateReader();
        using var message = Message.CreateMessage(messageVersion, action: null, reader);

        // Buffered: the reader above is disposed on return, and a streamed body would only be read
        // later, when the envelope is actually written.
        using var buffer = message.CreateBufferedCopy(MaxBufferSize);
        return buffer.CreateMessage();
    }

    private static string Write(Message message)
    {
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true,
        }))
        {
            message.WriteMessage(writer);
        }

        return builder.ToString();
    }
}
