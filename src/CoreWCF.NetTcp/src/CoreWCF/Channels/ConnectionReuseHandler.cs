// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class ConnectionReuseHandler : IConnectionReuseHandler
    {
        private readonly TcpTransportBindingElement _bindingElement;
        private int _maxPooledConnections;
        private TimeSpan _idleTimeout;
        private readonly SemaphoreSlim _connectionPoolSemaphore;

        public ConnectionReuseHandler(TcpTransportBindingElement bindingElement)
        {
            _bindingElement = bindingElement;
            Initialize(_bindingElement);
            _connectionPoolSemaphore = new SemaphoreSlim(_maxPooledConnections);
        }

        private void Initialize(TcpTransportBindingElement bindingElement)
        {
            int maxOutboundConnectionsPerEndpoint = bindingElement.ConnectionPoolSettings.MaxOutboundConnectionsPerEndpoint;
            if (maxOutboundConnectionsPerEndpoint == ConnectionOrientedTransportDefaults.MaxOutboundConnectionsPerEndpoint)
            {
                _maxPooledConnections = ConnectionOrientedTransportDefaults.GetMaxConnections();
            }
            else
            {
                _maxPooledConnections = maxOutboundConnectionsPerEndpoint;
            }

            _idleTimeout = bindingElement.ConnectionPoolSettings.IdleTimeout;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any exception means don't reuse connection, nothing else to do")]
        public async Task<bool> ReuseConnectionAsync(FramingConnection connection, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (!_connectionPoolSemaphore.Wait(0))
            {
                //if (DiagnosticUtility.ShouldTraceWarning)
                //{
                //    TraceUtility.TraceEvent(TraceEventType.Warning,
                //        TraceCode.ServerMaxPooledConnectionsQuotaReached,
                //        SR.GetString(SR.TraceCodeServerMaxPooledConnectionsQuotaReached, maxPooledConnections),
                //        new StringTraceRecord("MaxOutboundConnectionsPerEndpoint", maxPooledConnections.ToString(CultureInfo.InvariantCulture)),
                //        this, null);
                //}

                //if (TD.ServerMaxPooledConnectionsQuotaReachedIsEnabled())
                //{
                //    TD.ServerMaxPooledConnectionsQuotaReached();
                //}
                connection.Logger.ConnectionPoolFull();
                // No space left in the connection pool
                return false;
            }

            try
            {
                connection.Reset();

                CancellationToken ct = new TimeoutHelper(_idleTimeout).GetCancellationToken();
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    new TimeoutHelper(_idleTimeout).GetCancellationToken(),
                    cancellationToken))
                {
                    connection.Logger.StartPendingReadOnIdleSocket();
                    Debug.Assert(connection.Transport == connection.RawTransport);
                    var readResult = await connection.Input.ReadAsync(linkedCts.Token);
                    connection.Logger.EndPendingReadOnIdleSocket(readResult);
                    if (readResult.IsCompleted)
                    {
                        connection.Logger.IdleConnectionClosed();
                        connection.Output.Complete();
                        return false;
                    }

                    connection.Input.AdvanceTo(readResult.Buffer.Start); // Don't consume any bytes. The pending read is to know when a new client connects.
                }

                return true;
            }
            catch (Exception e)
            {
                connection.Logger.FailureInConnectionReuse(e);
                return false;
            }
            finally
            {
                _connectionPoolSemaphore.Release();
            }
        }
    }
}
