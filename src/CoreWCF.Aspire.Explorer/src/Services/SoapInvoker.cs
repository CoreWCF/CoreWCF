// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Aspire.Explorer.Model;

namespace CoreWCF.Aspire.Explorer.Services;

/// <summary>The outcome of invoking a SOAP operation.</summary>
/// <param name="StatusCode">HTTP status code returned by the service.</param>
/// <param name="ReasonPhrase">HTTP reason phrase.</param>
/// <param name="Body">The response envelope, indented.</param>
/// <param name="IsSuccess">Whether the call succeeded and did not return a fault.</param>
/// <param name="Elapsed">Round-trip duration.</param>
public sealed record SoapInvocationResult(
    int StatusCode,
    string? ReasonPhrase,
    string Body,
    bool IsSuccess,
    TimeSpan Elapsed);

/// <summary>
/// Sends a SOAP request to a service endpoint using the WCF client stack.
/// <para>
/// It goes through <see cref="IRequestChannel"/> rather than a typed <c>ChannelFactory&lt;T&gt;</c>
/// because the explorer has no compile-time contract to bind to - the operations come from whatever
/// WSDL was just read. That keeps WCF in charge of the envelope, the SOAP version and the action
/// header, while still accepting an arbitrary envelope the user has hand-edited.
/// </para>
/// </summary>
public sealed class SoapInvoker
{
    // Generous, because this is a development tool pointed at a developer's own service: a truncated
    // response would look like a bug in the service rather than a limit here.
    private const int MaxMessageSize = 64 * 1024 * 1024;

    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(100);

    public async Task<SoapInvocationResult> InvokeAsync(
        string endpointAddress,
        WsdlOperation operation,
        string envelopeXml,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointAddress);
        ArgumentNullException.ThrowIfNull(operation);

        var address = new EndpointAddress(endpointAddress);
        var binding = CreateBinding(address.Uri, operation.SoapVersion);

        var factory = new ChannelFactory<IRequestChannel>(binding, address);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var channel = factory.CreateChannel();

            // IRequestChannel predates cancellation tokens; aborting the channel is how an in-flight
            // request is torn down, and it surfaces below as a CommunicationException.
            using var abortOnCancel = cancellationToken.Register(() => Abort(channel));

            await Task.Factory.FromAsync(channel.BeginOpen, channel.EndOpen, null).ConfigureAwait(false);

            using var request = CreateRequest(envelopeXml, binding.MessageVersion, operation.SoapAction);
            var reply = await Task.Factory.FromAsync(
                (callback, state) => channel.BeginRequest(request, callback, state),
                channel.EndRequest,
                null).ConfigureAwait(false);

            stopwatch.Stop();

            // A one-way contract would legitimately reply with nothing at all.
            if (reply is null)
            {
                return new SoapInvocationResult(202, "Accepted", string.Empty, true, stopwatch.Elapsed);
            }

            using var buffer = reply.CreateBufferedCopy(MaxMessageSize);
            return BuildResult(buffer, stopwatch.Elapsed);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is not OperationCanceledException)
        {
            throw new OperationCanceledException("The invocation was cancelled.", ex, cancellationToken);
        }
        finally
        {
            Abort(factory);
        }
    }

    /// <summary>
    /// Builds the request from the envelope as it stands in the editor, so anything the user added -
    /// including headers - is sent as written. SOAP 1.1 carries no addressing headers, so the action
    /// is not in the envelope and has to be set for the transport to emit SOAPAction.
    /// </summary>
    private static Message CreateRequest(string envelopeXml, MessageVersion version, string soapAction)
    {
        using var reader = XmlReader.Create(new StringReader(envelopeXml));
        var message = Message.CreateMessage(reader, MaxMessageSize, version);

        if (string.IsNullOrEmpty(message.Headers.Action) && !string.IsNullOrEmpty(soapAction))
        {
            message.Headers.Action = soapAction;
        }

        // Buffered: the reader is disposed when this method returns, and a streamed message would
        // only be read later, when the channel actually writes it.
        using var buffer = message.CreateBufferedCopy(MaxMessageSize);
        return buffer.CreateMessage();
    }

    private static SoapInvocationResult BuildResult(MessageBuffer buffer, TimeSpan elapsed)
    {
        using var forProperties = buffer.CreateMessage();
        var isFault = forProperties.IsFault;

        // The HTTP transport hands the response's status line up as a message property, so moving to
        // a channel does not cost the status the UI reports.
        var statusCode = isFault ? 500 : 200;
        string? reasonPhrase = isFault ? "Internal Server Error" : "OK";

        if (forProperties.Properties.TryGetValue(HttpResponseMessageProperty.Name, out var property)
            && property is HttpResponseMessageProperty http)
        {
            statusCode = (int)http.StatusCode;
            reasonPhrase = string.IsNullOrEmpty(http.StatusDescription)
                ? http.StatusCode.ToString()
                : http.StatusDescription;
        }

        using var forBody = buffer.CreateMessage();
        return new SoapInvocationResult(
            statusCode,
            reasonPhrase,
            WriteEnvelope(forBody),
            statusCode < 400 && !isFault,
            elapsed);
    }

    private static string WriteEnvelope(Message message)
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

    private static Binding CreateBinding(Uri uri, SoapVersion soapVersion)
    {
        var transport = uri.Scheme == Uri.UriSchemeHttps
            ? new HttpsTransportBindingElement()
            : new HttpTransportBindingElement();
        transport.MaxReceivedMessageSize = MaxMessageSize;
        transport.MaxBufferSize = MaxMessageSize;

        // Addressing is deliberately off for both versions. The explorer targets whatever endpoint it
        // was given, and requiring WS-Addressing headers the service may not expect would break
        // plain BasicHttpBinding services - the common case.
        var envelope = soapVersion == SoapVersion.Soap12 ? EnvelopeVersion.Soap12 : EnvelopeVersion.Soap11;
        var encoding = new TextMessageEncodingBindingElement(
            MessageVersion.CreateVersion(envelope, AddressingVersion.None),
            Encoding.UTF8);
        encoding.ReaderQuotas.MaxStringContentLength = MaxMessageSize;
        encoding.ReaderQuotas.MaxArrayLength = MaxMessageSize;
        encoding.ReaderQuotas.MaxDepth = 128;

        return new CustomBinding(encoding, transport)
        {
            OpenTimeout = s_timeout,
            SendTimeout = s_timeout,
            ReceiveTimeout = s_timeout,
            CloseTimeout = s_timeout,
        };
    }

    /// <summary>Aborting never throws for the caller: it is cleanup, on a path that may already be failing.</summary>
    private static void Abort(ICommunicationObject communicationObject)
    {
        try
        {
            communicationObject.Abort();
        }
        catch (CommunicationException)
        {
        }
        catch (TimeoutException)
        {
        }
    }
}
