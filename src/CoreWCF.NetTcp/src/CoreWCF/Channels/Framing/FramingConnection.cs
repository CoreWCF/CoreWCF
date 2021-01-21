// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace CoreWCF.Channels.Framing
{
    public class FramingConnection
    {
        private ConnectionContext _context;
        public FramingConnection(ConnectionContext context)
        {
            _context = context;
            Transport = _context.Transport;
        }

        public MessageEncoderFactory MessageEncoderFactory { get; internal set; }
        public StreamUpgradeAcceptor StreamUpgradeAcceptor { get; internal set; }
        public ISecurityCapabilities SecurityCapabilities { get; internal set; }
        public IServiceDispatcher ServiceDispatcher { get; internal set; }
        public PipeReader Input => Transport.Input;
        public PipeWriter Output => Transport.Output;
        public IDuplexPipe Transport { get; internal set; }
        public IDuplexPipe RawTransport => _context.Transport;
        internal FramingDecoder FramingDecoder { get; set; }
        public Uri Via => FramingDecoder?.Via;
        internal FramingMode FramingMode { get; set; }
        public MessageEncoder MessageEncoder { get; internal set; }
        public SecurityMessageProperty SecurityMessageProperty
        {
            get;
            internal set;
        }
        public bool EOF { get; internal set; }
        public Memory<byte> EnvelopeBuffer { get; internal set; }
        public int EnvelopeOffset { get; internal set; }
        public BufferManager BufferManager { get; internal set; }
        public int EnvelopeSize { get; internal set; }
        public int MaxReceivedMessageSize { get; internal set; }
        public int MaxBufferSize { get; internal set; }
        public int ConnectionBufferSize { get; internal set; }
        public TransferMode TransferMode { get; internal set; }
        public RawStream RawStream { get; internal set; }
        public IPEndPoint RemoteEndpoint
        {
            get
            {
                var connectionFeature = _context.Features.Get<IHttpConnectionFeature>();
                if (connectionFeature == null)
                {
                    return null;
                }

                return new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);
            }
        }

        internal void Reset()
        {
            MessageEncoderFactory = null;
            StreamUpgradeAcceptor = null;
            SecurityCapabilities = null;
            ServiceDispatcher = null;
            Transport = RawTransport;
            FramingDecoder = null;
            FramingMode = default;
            MessageEncoder = null;
            SecurityMessageProperty = null;
            EOF = false;
            EnvelopeBuffer = null;
            EnvelopeOffset = 0;
            BufferManager = null;
            EnvelopeSize = 0;
            MaxReceivedMessageSize = 0;
            MaxBufferSize = 0;
            ConnectionBufferSize = 0;
            TransferMode = default;
            RawStream = null;
        }

        public void Abort() { _context.Abort(); }
        public void Abort(Exception e) { _context.Abort(new ConnectionAbortedException(e.Message, e)); }
        public void Abort(string reason) { _context.Abort(new ConnectionAbortedException(reason)); }

        public Task CloseAsync(TimeSpan timeout)
        {
            // Closing should be async and should accept a timeout. There are improvements coming in future releases of .NET Core which support this.
            Input.Complete();
            Output.Complete();
            return Task.CompletedTask;
        }

        public async Task SendFaultAsync(string faultString, TimeSpan sendTimeout, int maxRead)
        {
            //if (TD.ConnectionReaderSendFaultIsEnabled())
            //{
            //    TD.ConnectionReaderSendFault(faultString);
            //}
            var encodedFault = new EncodedFault(faultString);
            var timeoutHelper = new TimeoutHelper(sendTimeout);
            var ct = timeoutHelper.GetCancellationToken();
            try
            {
                await Output.WriteAsync(encodedFault.EncodedBytes, ct);
                await Output.FlushAsync();
                // Connection will be closed on completion of Task returned from NetMessageFramingConnectionHandler.OnConnectedAsync
            }
            catch (CommunicationException e) // TODO: Consider exception filters to remvoe duplicate code
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                Abort(e);
                return;
            }
            catch (OperationCanceledException e)
            {
                //if (TD.SendTimeoutIsEnabled())
                //{
                //    TD.SendTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                Abort(e);
                return;
            }
            catch (TimeoutException e)
            {
                //if (TD.SendTimeoutIsEnabled())
                //{
                //    TD.SendTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                Abort(e);
                return;
            }

            // make sure we read until EOF or a quota is hit
            ReadResult readResult;
            long readTotal = 0;
            for (; ; )
            {
                try
                {
                    readResult = await Input.ReadAsync(ct);
                }
                catch (CommunicationException e) // TODO: Exception filters?
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    Abort(e);
                    return;
                }
                // TODO: Standardize handling of OperationCanceledException/TimeoutException
                catch (OperationCanceledException e)
                {
                    //if (TD.SendTimeoutIsEnabled())
                    //{
                    //    TD.SendTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    Abort(e);
                    return;
                }
                catch (TimeoutException e)
                {
                    //if (TD.SendTimeoutIsEnabled())
                    //{
                    //    TD.SendTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    Abort(e);
                    return;
                }

                if (readResult.IsCompleted)
                    break;

                readTotal += readResult.Buffer.Length;
                Input.AdvanceTo(readResult.Buffer.End);
                if (readTotal > maxRead || timeoutHelper.RemainingTime() <= TimeSpan.Zero)
                {
                    Abort();
                    return;
                }
            }
        }
    }
}
