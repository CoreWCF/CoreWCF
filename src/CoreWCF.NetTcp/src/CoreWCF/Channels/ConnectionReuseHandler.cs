// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        private readonly int _pooledConnectionCount;
        private readonly SemaphoreSlim _connectionPoolSemaphore;

        public ConnectionReuseHandler(TcpTransportBindingElement bindingElement)
        {
            _bindingElement = bindingElement;
            Initialize(_bindingElement);
            _connectionPoolSemaphore = new SemaphoreSlim(_maxPooledConnections);
        }

        private void Initialize(TcpTransportBindingElement bindingElement)
        {
            var maxOutboundConnectionsPerEndpoint = bindingElement.ConnectionPoolSettings.MaxOutboundConnectionsPerEndpoint;
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

                // No space left in the connection pool
                return false;
            }

            try
            {
                connection.Reset();

                var ct = new TimeoutHelper(_idleTimeout).GetCancellationToken();
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    new TimeoutHelper(_idleTimeout).GetCancellationToken(),
                    cancellationToken))
                {
                    var readResult = await connection.Input.ReadAsync(linkedCts.Token);
                    connection.Input.AdvanceTo(readResult.Buffer.Start); // Don't consume any bytes. The pending read is to know when a new client connects.
                    if (readResult.Buffer.IsEmpty && !readResult.IsCompleted && !readResult.IsCanceled)
                    {
                        // After pending read is canceled, next ReadAsync can return immediately with a 0 byte response so another ReadAsync call is needed
                        readResult = await connection.Input.ReadAsync(linkedCts.Token);
                        connection.Input.AdvanceTo(readResult.Buffer.Start); // Don't consume any bytes. The pending read is to know when a new client connects.
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _connectionPoolSemaphore.Release();
            }
        }
    }
}
