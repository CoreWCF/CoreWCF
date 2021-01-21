// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class DuplexFramingMiddleware
    {
        private readonly HandshakeDelegate _next;

        public DuplexFramingMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            var decoder = new ServerSessionDecoder(ConnectionOrientedTransportDefaults.MaxViaSize, ConnectionOrientedTransportDefaults.MaxContentTypeSize);
            bool success = false;

            try
            {
                ReadOnlySequence<byte> buffer;
                while (decoder.CurrentState != ServerSessionDecoder.State.PreUpgradeStart)
                {
                    var readResult = await connection.Input.ReadAsync();
                    buffer = readResult.Buffer;

                    while (buffer.Length > 0)
                    {
                        int bytesDecoded = decoder.Decode(buffer);
                        if (bytesDecoded > 0)
                        {
                            buffer = buffer.Slice(bytesDecoded);
                        }

                        if (decoder.CurrentState == ServerSessionDecoder.State.PreUpgradeStart)
                        {
                            // We now know the Via address (which endpoint the client is connecting to).
                            // The connection now needs to be handled by the correct endpoint which can
                            // handle upgrades etc.
                            break; //exit loop
                        }
                    }

                    connection.Input.AdvanceTo(buffer.Start);
                }

                success = true;
            }
            catch (CommunicationException exception)
            {
                DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }
            catch (OperationCanceledException exception)
            {
                //if (TD.ReceiveTimeoutIsEnabled())
                //{
                //    TD.ReceiveTimeout(exception.Message);
                //}
                DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }
            catch (TimeoutException exception)
            {
                //if (TD.ReceiveTimeoutIsEnabled())
                //{
                //    TD.ReceiveTimeout(exception.Message);
                //}
                DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (!TransportExceptionHandler.HandleTransportExceptionHelper(e))
                {
                    throw;
                }
                // containment -- all exceptions abort the reader, no additional containment action necessary
            }
            finally
            {
                if (!success)
                {
                    // TODO: On .NET Framework, this Abort call via a long winding path of plumbing will trigger a new pending Accept
                    // as this connection establishment handshake has failed. Some back pressure mechanism needs to be implemented to
                    // stop extra incoming connection handshakes from being started. Maybe a semaphore which is async waited at initial
                    // incoming request and then released on completion of handshake or on an exception. It also closes the connection
                    // so that's all that's happening here for now. Returning and completing the task will cause the connection to be closed.
                    connection.Abort();
                }
            }

            if (success)
            {
                connection.FramingDecoder = decoder;
                await _next(connection);
            }
            // else:
            //   returning will close the connection if it hasn't already been.
        }
    }
}