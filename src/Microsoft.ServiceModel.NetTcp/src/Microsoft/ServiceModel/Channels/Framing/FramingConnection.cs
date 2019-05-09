using Microsoft.AspNetCore.Connections;
using Microsoft.ServiceModel.Configuration;
using Microsoft.ServiceModel.Security;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels.Framing
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
        internal ServerSessionDecoder ServerSessionDecoder { get; set; }
        public Uri Via => ServerSessionDecoder?.Via;
        internal FramingMode FramingMode { get; set; }
        public MessageEncoder MessageEncoder { get; internal set; }
        public SecurityMessageProperty SecurityMessageProperty { get; internal set; }
        public bool EOF { get; internal set; }
        public Memory<byte> EnvelopeBuffer { get; internal set; }
        public int EnvelopeOffset { get; internal set; }
        public BufferManager BufferManager { get; internal set; }
        public int EnvelopeSize { get; internal set; }
    }
}
