// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Pipelines;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class StreamDuplexPipe : IDuplexPipe
    {
        private static readonly StreamPipeReaderOptions s_readerOpts = new StreamPipeReaderOptions(leaveOpen: true);
        private static readonly StreamPipeWriterOptions s_writerOpts = new StreamPipeWriterOptions(leaveOpen: true);

        public StreamDuplexPipe(IDuplexPipe transport, Stream stream)
        {
            Stream = stream;
            Input = PipeReader.Create(stream, s_readerOpts);
            Output = PipeWriter.Create(stream, s_writerOpts);
        }

        public Stream Stream { get; }
        public PipeReader Input { get; }
        public PipeWriter Output { get; }
    }
}
