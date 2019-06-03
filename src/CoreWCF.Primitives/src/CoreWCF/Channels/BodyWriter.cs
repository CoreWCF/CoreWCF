using System;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels
{
    public abstract class BodyWriter
    {
        bool isBuffered;
        bool canWrite;
        object thisLock;

        protected BodyWriter(bool isBuffered)
        {
            this.isBuffered = isBuffered;
            canWrite = true;
            if (!this.isBuffered)
            {
                thisLock = new object();
            }
        }

        public bool IsBuffered
        {
            get { return isBuffered; }
        }

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferSize), maxBufferSize,
                                                    SR.ValueMustBeNonNegative));
            if (isBuffered)
            {
                return this;
            }
            else
            {
                lock (thisLock)
                {
                    if (!canWrite)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BodyWriterCanOnlyBeWrittenOnce));
                    canWrite = false;
                }
                BodyWriter bodyWriter = OnCreateBufferedCopy(maxBufferSize);
                if (!bodyWriter.IsBuffered)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BodyWriterReturnedIsNotBuffered));
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

        void EnsureWriteBodyContentsState(XmlDictionaryWriter writer)
        {
            if (writer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            if (!isBuffered)
            {
                lock (thisLock)
                {
                    if (!canWrite)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BodyWriterCanOnlyBeWrittenOnce));
                    canWrite = false;
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

        class BufferedBodyWriter : BodyWriter
        {
            XmlBuffer buffer;

            public BufferedBodyWriter(XmlBuffer buffer)
                : base(true)
            {
                this.buffer = buffer;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                XmlDictionaryReader reader = buffer.GetReader(0);
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