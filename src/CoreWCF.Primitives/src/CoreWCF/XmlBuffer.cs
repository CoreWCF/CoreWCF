﻿using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal class XmlBuffer
    {
        List<Section> _sections;
        byte[] _buffer;
        int _offset;
        BufferedOutputStream _stream;
        BufferState _bufferState;
        XmlDictionaryWriter _writer;
        XmlDictionaryReaderQuotas _quotas;

        enum BufferState
        {
            Created,
            Writing,
            Reading,
        }

        struct Section
        {
            int _offset;
            int _size;
            XmlDictionaryReaderQuotas _quotas;

            public Section(int offset, int size, XmlDictionaryReaderQuotas quotas)
            {
                _offset = offset;
                _size = size;
                _quotas = quotas;
            }

            public int Offset
            {
                get { return _offset; }
            }

            public int Size
            {
                get { return _size; }
            }

            public XmlDictionaryReaderQuotas Quotas
            {
                get { return _quotas; }
            }
        }

        public XmlBuffer(int maxBufferSize)
        {
            if (maxBufferSize < 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferSize), maxBufferSize,
                                                    SR.ValueMustBeNonNegative));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            _bufferState = BufferState.Writing;
            _quotas = new XmlDictionaryReaderQuotas();
            quotas.CopyTo(_quotas);
            if (_writer != null)

            {

                // We always want to Dispose of the writer now; previously, writers could be reassigned 

                // to a new stream, with a new dictionary and session. 

                var thisWriter = _writer;

                thisWriter.Dispose();

                _writer = null;

            }

            _writer = XmlDictionaryWriter.CreateBinaryWriter(_stream, XD.Dictionary, null, true);
            return _writer;
        }

        public void CloseSection()
        {
            if (_bufferState != BufferState.Writing)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            _bufferState = BufferState.Reading;
            int bufferSize;
            _buffer = _stream.ToArray(out bufferSize);
            _writer = null;
            _stream = null;
        }

        Exception CreateInvalidStateException()
        {
            return new InvalidOperationException(SR.XmlBufferInInvalidState);
        }

        public XmlDictionaryReader GetReader(int sectionIndex)
        {
            if (_bufferState != BufferState.Reading)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            Section section = _sections[sectionIndex];
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(_buffer, section.Offset, section.Size, XD.Dictionary, section.Quotas);
            reader.MoveToContent();
            return reader;
        }

        public void WriteTo(int sectionIndex, XmlWriter writer)
        {
            if (_bufferState != BufferState.Reading)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
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