// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal class XmlBuffer
    {
        private readonly List<Section> _sections;
        private byte[] _buffer;
        private int _offset;
        private BufferedOutputStream _stream;
        private BufferState _bufferState;
        private XmlDictionaryWriter _writer;
        private XmlDictionaryReaderQuotas _quotas;

        private enum BufferState
        {
            Created,
            Writing,
            Reading,
        }

        private struct Section
        {
            public Section(int offset, int size, XmlDictionaryReaderQuotas quotas)
            {
                Offset = offset;
                Size = size;
                Quotas = quotas;
            }

            public int Offset { get; }

            public int Size { get; }

            public XmlDictionaryReaderQuotas Quotas { get; }
        }

        public XmlBuffer(int maxBufferSize)
        {
            if (maxBufferSize < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferSize), maxBufferSize,
                                                    SRCommon.ValueMustBeNonNegative));
            }

            int initialBufferSize = Math.Min(512, maxBufferSize);
            _stream = new BufferManagerOutputStream(SR.XmlBufferQuotaExceeded, initialBufferSize, maxBufferSize,
                BufferManager.CreateBufferManager(0, int.MaxValue));
            _sections = new List<Section>(1);
        }

        public int BufferSize
        {
            get
            {
                Fx.Assert(_bufferState == BufferState.Reading, "Buffer size should only be retrieved during Reading state");
                return _buffer.Length;
            }
        }

        public int SectionCount
        {
            get { return _sections.Count; }
        }

        public XmlDictionaryWriter OpenSection(XmlDictionaryReaderQuotas quotas)
        {
            if (_bufferState != BufferState.Created)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            }

            _bufferState = BufferState.Writing;
            _quotas = new XmlDictionaryReaderQuotas();
            quotas.CopyTo(_quotas);
            if (_writer != null)

            {
                // We always want to Dispose of the writer now; previously, writers could be reassigned 

                // to a new stream, with a new dictionary and session. 

                XmlDictionaryWriter thisWriter = _writer;

                thisWriter.Dispose();

                _writer = null;
            }

            _writer = XmlDictionaryWriter.CreateBinaryWriter(_stream, XD.Dictionary, null, true);
            return _writer;
        }

        public void CloseSection()
        {
            if (_bufferState != BufferState.Writing)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            }

            _writer.Dispose();
            _writer = null;
            _bufferState = BufferState.Created;
            int size = (int)_stream.Length - _offset;
            _sections.Add(new Section(_offset, size, _quotas));
            _offset += size;
        }

        public void Close()
        {
            if (_bufferState != BufferState.Created)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            }

            _bufferState = BufferState.Reading;
            _buffer = _stream.ToArray(out int bufferSize);
            _writer = null;
            _stream = null;
        }

        private Exception CreateInvalidStateException()
        {
            return new InvalidOperationException(SR.XmlBufferInInvalidState);
        }

        public XmlDictionaryReader GetReader(int sectionIndex)
        {
            if (_bufferState != BufferState.Reading)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            }

            Section section = _sections[sectionIndex];
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(_buffer, section.Offset, section.Size, XD.Dictionary, section.Quotas);
            reader.MoveToContent();
            return reader;
        }

        public void WriteTo(int sectionIndex, XmlWriter writer)
        {
            if (_bufferState != BufferState.Reading)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            }

            XmlDictionaryReader reader = GetReader(sectionIndex);
            try
            {
                writer.WriteNode(reader, false);
            }
            finally
            {
                reader.Dispose();
            }
        }
    }
}