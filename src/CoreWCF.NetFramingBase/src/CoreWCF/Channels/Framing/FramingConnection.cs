// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoreWCF.Channels.Framing
{
    public class FramingConnection
    {
        private readonly ConnectionContext _context;

        public FramingConnection(ConnectionContext context)
        {
            _context = context;
            Logger = context.Features.Get<ILogger>();
            if (Logger== null)
            {
                Logger = NullLogger.Instance;
            }

            Transport = RawTransport = _context.Transport;
            RemoteEndpoint = GetRemoteEndPoint(context);
        }

        public MessageEncoderFactory MessageEncoderFactory { get; internal set; }
        public StreamUpgradeAcceptor StreamUpgradeAcceptor { get; internal set; }
        public ISecurityCapabilities SecurityCapabilities { get; internal set; }
        public IServiceDispatcher ServiceDispatcher { get; internal set; }
        public PipeReader Input => Transport.Input;
        public PipeWriter Output => Transport.Output;
        public IDuplexPipe Transport { get; set; }
        public IDuplexPipe RawTransport { get; private set; }
        internal FramingDecoder FramingDecoder { get; set; }
        public Uri Via => FramingDecoder?.Via;
        internal FramingMode FramingMode { get; set; }
        public MessageEncoder MessageEncoder { get; internal set; }
        public IFeatureCollection ConnectionFeatures => _context.Features;
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
        public long MaxReceivedMessageSize { get; internal set; }
        public int MaxBufferSize { get; internal set; }
        public int ConnectionBufferSize { get; internal set; }
        public TransferMode TransferMode { get; internal set; }
        internal Stream RawStream { get; set; }
        public ILogger Logger { get; }
        public IPEndPoint RemoteEndpoint { get; }

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
            System.Threading.CancellationToken ct = timeoutHelper.GetCancellationToken();
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
                {
                    break;
                }

                readTotal += readResult.Buffer.Length;
                Input.AdvanceTo(readResult.Buffer.End);
                if (readTotal > maxRead || timeoutHelper.RemainingTime() <= TimeSpan.Zero)
                {
                    Abort();
                    return;
                }
            }
        }

        /// <summary>
        /// Tries to extract the remote endpoint from the given <see cref="ConnectionContext"/> in a
        /// version agnostic way.
        /// </summary>
        /// <param name="context">The ASP.net core connection context to extract the remote endpoint from.</param>
        /// <returns>The endpoint of the remote party or null if it was not provided.</returns>
        private static IPEndPoint GetRemoteEndPoint(ConnectionContext context)
        {
            // 1st chance: Server might provide remote endpoint via HTTP feature
            // (mostly the case in ASP.net core v2.x)
            IHttpConnectionFeature connectionFeature = context.Features.Get<IHttpConnectionFeature>();
            if (connectionFeature != null)
            {
                return new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);
            }

            // 2nd chance: on ASP.net core 5.0 the ConnectionContext has a direct Property RemoteEndpoint
            // via baseclass.
            var net5RemoteEndPointPropertyAccessor = s_net5RemoteEndPointPropertyAccessor;
            if (net5RemoteEndPointPropertyAccessor != null)
            {
                return net5RemoteEndPointPropertyAccessor(context);
            }

            // last chance: server does likely not support access to remote endpoint. could be
            // a non-tcp server like the ASP.net core test server
            return null;
        }


        private static readonly Func<ConnectionContext, IPEndPoint> s_net5RemoteEndPointPropertyAccessor =
            BuildNet5RemoteEndPointPropertyAccessor();

        private static Func<ConnectionContext, IPEndPoint> BuildNet5RemoteEndPointPropertyAccessor()
        {
            // https://github.com/dotnet/aspnetcore/blob/v5.0.9/src/Servers/Connections.Abstractions/src/BaseConnectionContext.cs
            var property =
                typeof(ConnectionContext).GetProperty("RemoteEndPoint", BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return null;
            }

            // context => context.RemoteEndPoint as IPEndpoint
            var contextParam = Expression.Parameter(typeof(ConnectionContext), "context");
            return Expression.Lambda<Func<ConnectionContext, IPEndPoint>>(
                 Expression.TypeAs(Expression.Property(contextParam, property),  typeof(IPEndPoint)),
                contextParam
            ).Compile();
        }
    }
}
