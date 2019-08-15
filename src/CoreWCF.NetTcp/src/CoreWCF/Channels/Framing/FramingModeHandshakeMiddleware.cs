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
        }
    }
}