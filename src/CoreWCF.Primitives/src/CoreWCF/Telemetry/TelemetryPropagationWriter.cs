// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Telemetry;

/// <summary>
/// Pre-defined PropagationWriter callbacks.
/// </summary>
internal static class TelemetryPropagationWriter
{
    /// <summary>
    /// Writes the values to SOAP message headers. If the message is not a SOAP message it does nothing.
    /// </summary>
    /// <param name="request">The outgoing request <see cref="Message"/> (carrier) to write trace context to.</param>
    /// <param name="name">The header name being written.</param>
    /// <param name="value">The header value to write.</param>
    public static void SoapMessageHeaders(Message request, string name, string value)
    {
        if (request.Version == MessageVersion.None)
        {
            return;
        }

        request.Headers.Add(TelemetryMessageHeader.CreateHeader(name, value));
    }

    /// <summary>
    /// Writes the values to outgoing HTTP request headers. If the message is not ultimately transmitted via HTTP these will be ignored.
    /// </summary>
    /// <param name="request">The outgoing request <see cref="Message"/> (carrier) to write trace context to.</param>
    /// <param name="name">The header name being written.</param>
    /// <param name="value">The header value to write.</param>
    public static void HttpRequestHeaders(Message request, string name, string value)
    {
        if (!HttpRequestMessagePropertyWrapper.IsHttpFunctionalityEnabled)
        {
            return;
        }

        if (!request.Properties.TryGetValue(HttpRequestMessagePropertyWrapper.Name, out var prop))
        {
            prop = HttpRequestMessagePropertyWrapper.CreateNew();
            request.Properties.Add(HttpRequestMessagePropertyWrapper.Name, prop);
        }

        HttpRequestMessagePropertyWrapper.GetHeaders(prop)[name] = value;
    }

    /// <summary>
    /// Writes values to both SOAP message headers and outgoing HTTP request headers when suppressing downstream instrumentation.
    /// When not suppressing downstream instrumentation it is assumed the transport will be propagating the context itself, so
    /// only SOAP message headers are written.
    /// </summary>
    /// <param name="request">The outgoing request <see cref="Message"/> (carrier) to write trace context to.</param>
    /// <param name="name">The header name being written.</param>
    /// <param name="value">The header value to write.</param>
    public static void Default(Message request, string name, string value)
    {
        SoapMessageHeaders(request, name, value);
        if (WcfInstrumentationActivitySource.Options?.SuppressDownstreamInstrumentation ?? false)
        {
            HttpRequestHeaders(request, name, value);
        }
    }

    /// <summary>
    /// Compose multiple PropagationWriter callbacks into a single callback. All callbacks
    /// are executed to allow for propagating the context in multiple ways.
    /// </summary>
    /// <param name="callbacks">The callbacks to compose into a single callback.</param>
    /// <returns>The composed callback.</returns>
    public static Action<Message, string, string> Compose(params Action<Message, string, string>[] callbacks)
    {
        return (Message request, string name, string value) =>
        {
            foreach (var writer in callbacks)
            {
                writer(request, name, value);
            }
        };
    }
}
