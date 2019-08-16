using Microsoft.AspNetCore.Connections;
using CoreWCF.Configuration;
using System.Threading.Tasks;
using System;
using System.Threading;
using CoreWCF.Runtime;
using System.Diagnostics;
using System.IO.Pipelines;

namespace CoreWCF.Channels.Framing
{
    internal class FramingModeHandshakeMiddleware
    {
        private HandshakeDelegate _next;

        public FramingModeHandshakeMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            var inputPipe = connection.Input;
            var modeDecoder = new ServerModeDecoder();
            try
            {
                await modeDecoder.ReadModeAsync(inputPipe);
            }
            catch (CommunicationException e)
            {
                // see if we need to send back a framing fault
                string framingFault;
                if (FramingEncodingString.TryGetFaultString(e, out framingFault))
                {
                    // TODO: Timeouts
                    await connection.SendFaultAsync(framingFault, Timeout.InfiniteTimeSpan/*GetRemainingTimeout()*/,
                         ConnectionOrientedTransportDefaults.MaxViaSize + ConnectionOrientedTransportDefaults.MaxContentTypeSize);
                }

                return; // Completing the returned Task causes the connection to be closed if needed and cleans everything up.
            }

            connection.FramingMode = modeDecoder.Mode;
            await _next(connection);

            // One .NET Framework, with the way that AspNetCore closes a connection, it sometimes doesn't send the
            // final bytes if those bytes haven't been sent yet. Delaying completeing the connection to compensate.
            await Task.Delay(5);

            // AspNetCore 2.1 doesn't close the connection. 2.2+ does so these lines can eventually be rmoved.
            connection.RawTransport.Input.Complete();
            connection.RawTransport.Output.Complete();
        }
    }
}