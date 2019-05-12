using Microsoft.AspNetCore.Connections;
using CoreWCF.Configuration;
using System.Buffers;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    internal class DuplexFramingMiddleware
    {
        private HandshakeDelegate _next;

        public DuplexFramingMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            var decoder = new ServerSessionDecoder(ConnectionOrientedTransportDefaults.MaxViaSize, ConnectionOrientedTransportDefaults.MaxContentTypeSize);
            ReadOnlySequence<byte> buffer;
            while (decoder.CurrentState != ServerSessionDecoder.State.PreUpgradeStart)
            {
                var readResult = await connection.Input.ReadAsync();
                buffer = readResult.Buffer;

                while (buffer.Length > 0)
                {
                    int bytesDecoded;
                    try
                    {
                        bytesDecoded = decoder.Decode(buffer);
                    }
                    catch (CommunicationException e)
                    {
                        // see if we need to send back a framing fault
                        string framingFault;
                        if (FramingEncodingString.TryGetFaultString(e, out framingFault))
                        {
                            // TODO: Drain the rest of the data and send a fault then close the connection
                            //byte[] drainBuffer = new byte[128];
                            //InitialServerConnectionReader.SendFault(
                            //    Connection, framingFault, drainBuffer, GetRemainingTimeout(),
                            //    MaxViaSize + MaxContentTypeSize);
                            //base.Close(GetRemainingTimeout());
                        }
                        throw;
                    }

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

            connection.ServerSessionDecoder = decoder;

            await _next(connection);
        }
    }
}