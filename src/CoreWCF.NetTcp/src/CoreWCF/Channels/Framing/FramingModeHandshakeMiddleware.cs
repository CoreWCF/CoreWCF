// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace CoreWCF.Channels.Framing
{
    internal class FramingModeHandshakeMiddleware
    {
        private readonly HandshakeDelegate _next;
        private readonly IApplicationLifetime _appLifetime;

        public FramingModeHandshakeMiddleware(HandshakeDelegate next, IApplicationLifetime appLifetime)
        {
            _next = next;
            _appLifetime = appLifetime;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            using (_appLifetime.ApplicationStopped.Register(() =>
             {
                 connection.RawTransport.Input.Complete();
                 connection.RawTransport.Output.Complete();
             }))
            {
                IConnectionReuseHandler reuseHandler = null;
                do
                {
                    System.IO.Pipelines.PipeReader inputPipe = connection.Input;
                    var modeDecoder = new ServerModeDecoder();
                    try
                    {
                        if (!await modeDecoder.ReadModeAsync(inputPipe))
                        {
                            break; // Input pipe closed
                        }
                    }
                    catch (CommunicationException e)
                    {
                        // see if we need to send back a framing fault
                        if (FramingEncodingString.TryGetFaultString(e, out string framingFault))
                        {
                            // TODO: Timeouts
                            await connection.SendFaultAsync(framingFault, Timeout.InfiniteTimeSpan/*GetRemainingTimeout()*/,
                                 ConnectionOrientedTransportDefaults.MaxViaSize + ConnectionOrientedTransportDefaults.MaxContentTypeSize);
                        }

                        return; // Completing the returned Task causes the connection to be closed if needed and cleans everything up.
                    }

                    connection.FramingMode = modeDecoder.Mode;
                    await _next(connection);

                    // Unwrap the connection.
                    // TODO: Investigate calling Dispose on the wrapping stream to improve cleanup. nb: .NET Framework does not call Dispose.
                    connection.Transport = connection.RawTransport;
                    // connection.ServiceDispatcher is null until later middleware layers are executed.
                    if (reuseHandler == null)
                    {
                        reuseHandler = connection.ServiceDispatcher.Binding.GetProperty<IConnectionReuseHandler>(new BindingParameterCollection());
                    }
                } while (await reuseHandler.ReuseConnectionAsync(connection, _appLifetime.ApplicationStopping));

                // On .NET Framework, with the way that AspNetCore closes a connection, it sometimes doesn't send the
                // final bytes if those bytes haven't been sent yet. Delaying completeing the connection to compensate.
                await Task.Delay(5);
            }

            // AspNetCore 2.1 doesn't close the connection. 2.2+ does so these lines can eventually be removed.
            connection.RawTransport.Input.Complete();
            connection.RawTransport.Output.Complete();
        }
    }
}
