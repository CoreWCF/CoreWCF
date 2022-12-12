// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels
{
    public abstract class BodyWriter
    {
        private bool _canWrite;
        private readonly object _thisLock;

        protected BodyWriter(bool isBuffered)
        {
            IsBuffered = isBuffered;
            _canWrite = true;
            if (!IsBuffered)
            {
                _thisLock = new object();
            }
        }

        public bool IsBuffered { get; }

        internal virtual bool IsEmpty
        {
            get { return false; }
        }

        internal virtual bool IsFault
        {
            get { return false; }
        }

        public BodyWriter CreateBufferedCopy(int maxBufferSize)
        {
            if (maxBufferSize < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferSize), maxBufferSize,
                                                    SRCommon.ValueMustBeNonNegative));
            }

            if (IsBuffered)
            {
                return this;
            }
            else
            {
                lock (_thisLock)
                {
                    if (!_canWrite)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BodyWriterCanOnlyBeWrittenOnce));
                    }

                    _canWrite = false;
                }
                BodyWriter bodyWriter = OnCreateBufferedCopy(maxBufferSize);
                if (!bodyWriter.IsBuffered)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BodyWriterReturnedIsNotBuffered));
                }

                return bodyWriter;
            }
        }

        protected virtual BodyWriter OnCreateBufferedCopy(int maxBufferSize)
        {
            return OnCreateBufferedCopy(maxBufferSize, XmlDictionaryReaderQuotas.Max);
        }

        internal BodyWriter OnCreateBufferedCopy(int maxBufferSize, XmlDictionaryReaderQuotas quotas)
        {
            XmlBuffer buffer = new XmlBuffer(maxBufferSize);
            using (XmlDictionaryWriter writer = buffer.OpenSection(quotas))
            {
                writer.WriteStartElement("a");
                OnWriteBodyContents(writer);
                writer.WriteEndElement();
            }
            buffer.CloseSection();
            buffer.Close();
            return new BufferedBodyWriter(buffer);
        }

        protected abstract void OnWriteBodyContents(XmlDictionaryWriter writer);

        protected virtual Task OnWriteBodyContentsAsync(XmlDictionaryWriter writer)
        {
            OnWriteBodyContents(writer);
            return Task.CompletedTask;
        }

        private void EnsureWriteBodyContentsState(XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (!IsBuffered)
            {
                lock (_thisLock)
                {
                    if (!_canWrite)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BodyWriterCanOnlyBeWrittenOnce));
                    }

                    _canWrite = false;
                }
            }
        }

        public void WriteBodyContents(XmlDictionaryWriter writer)
        {
            EnsureWriteBodyContentsState(writer);
            OnWriteBodyContents(writer);
        }

        public Task WriteBodyContentsAsync(XmlDictionaryWriter writer)
        {
            EnsureWriteBodyContentsState(writer);
            return OnWriteBodyContentsAsync(writer);
        }

        private class BufferedBodyWriter : BodyWriter
        {
            private readonly XmlBuffer _buffer;

            public BufferedBodyWriter(XmlBuffer buffer)
                : base(true)
            {
                _buffer = buffer;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                XmlDictionaryReader reader = _buffer.GetReader(0);
                using (reader)
                {
                    reader.ReadStartElement();
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        writer.WriteNode(reader, false);
                    }
                    reader.ReadEndElement();
                }
            }
        }
    }
}