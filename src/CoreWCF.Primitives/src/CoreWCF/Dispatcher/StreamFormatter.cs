// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class StreamFormatter
    {
        private string wrapperName;
        private string wrapperNS;
        private readonly string partName;
        private readonly string partNS;
        private readonly int streamIndex;
        private readonly bool isRequest;
        private readonly string operationName;
        private const int returnValueIndex = -1;

        internal static StreamFormatter Create(MessageDescription messageDescription, string operationName, bool isRequest)
        {
            MessagePartDescription streamPart = ValidateAndGetStreamPart(messageDescription, isRequest, operationName);
            if (streamPart == null)
            {
                return null;
            }

            return new StreamFormatter(messageDescription, streamPart, operationName, isRequest);
        }

        private StreamFormatter(MessageDescription messageDescription, MessagePartDescription streamPart, string operationName, bool isRequest)
        {
            if ((object)streamPart == (object)messageDescription.Body.ReturnValue)
            {
                streamIndex = returnValueIndex;
            }
            else
            {
                streamIndex = streamPart.Index;
            }

            wrapperName = messageDescription.Body.WrapperName;
            wrapperNS = messageDescription.Body.WrapperNamespace;
            partName = streamPart.Name;
            partNS = streamPart.Namespace;
            this.isRequest = isRequest;
            this.operationName = operationName;
        }

        internal void Serialize(XmlDictionaryWriter writer, object[] parameters, object returnValue)
        {
            Stream streamValue = GetStreamAndWriteStartWrapperIfNecessary(writer, parameters, returnValue);
            var streamProvider = new OperationStreamProvider(streamValue);
            StreamFormatterHelper.WriteValue(writer, streamProvider);
            WriteEndWrapperIfNecessary(writer);
        }

        internal async Task SerializeAsync(XmlDictionaryWriter writer, object[] parameters, object returnValue)
        {
            using (TaskHelpers.RunTaskContinuationsOnOurThreads()) // If inner stream doesn't have sync implementation, don't continue on thread pool.
            {
                // TODO: For NetStandard 2.0, use async methods on writer
                Stream streamValue = GetStreamAndWriteStartWrapperIfNecessary(writer, parameters, returnValue);
                var streamProvider = new OperationStreamProvider(streamValue);
                await StreamFormatterHelper.WriteValueAsync(writer, streamProvider);
                await WriteEndWrapperIfNecessaryAsync(writer);
            }
        }

        private Stream GetStreamAndWriteStartWrapperIfNecessary(XmlDictionaryWriter writer, object[] parameters, object returnValue)
        {
            Stream streamValue = GetStreamValue(parameters, returnValue);
            if (streamValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(partName);
            }

            if (WrapperName != null)
            {
                writer.WriteStartElement(null, WrapperName, WrapperNamespace);
            }

            writer.WriteStartElement(null, PartName, PartNamespace);
            return streamValue;
        }

        private void WriteEndWrapperIfNecessary(XmlDictionaryWriter writer)
        {
            writer.WriteEndElement();
            if (wrapperName != null)
            {
                writer.WriteEndElement();
            }
        }

        private Task WriteEndWrapperIfNecessaryAsync(XmlDictionaryWriter writer)
        {
            writer.WriteEndElement();
            if (wrapperName != null)
            {
                writer.WriteEndElement();
            }

            return Task.CompletedTask;
        }

        internal void Deserialize(object[] parameters, ref object retVal, Message message)
        {
            SetStreamValue(parameters, ref retVal, new MessageBodyStream(message, WrapperName, WrapperNamespace, PartName, PartNamespace, isRequest));
        }

        internal string WrapperName
        {
            get { return wrapperName; }
            set { wrapperName = value; }
        }

        internal string WrapperNamespace
        {
            get { return wrapperNS; }
            set { wrapperNS = value; }
        }

        internal string PartName
        {
            get { return partName; }
        }

        internal string PartNamespace
        {
            get { return partNS; }
        }

        private Stream GetStreamValue(object[] parameters, object returnValue)
        {
            if (streamIndex == returnValueIndex)
            {
                return (Stream)returnValue;
            }

            return (Stream)parameters[streamIndex];
        }

        private void SetStreamValue(object[] parameters, ref object returnValue, Stream streamValue)
        {
            if (streamIndex == returnValueIndex)
            {
                returnValue = streamValue;
            }
            else
            {
                parameters[streamIndex] = streamValue;
            }
        }

        private static MessagePartDescription ValidateAndGetStreamPart(MessageDescription messageDescription, bool isRequest, string operationName)
        {
            MessagePartDescription part = GetStreamPart(messageDescription);
            if (part != null)
            {
                return part;
            }

            if (HasStream(messageDescription))
            {
                if (messageDescription.IsTypedMessage)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInvalidStreamInTypedMessage, messageDescription.MessageName)));
                }
                else if (isRequest)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInvalidStreamInRequest, operationName)));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInvalidStreamInResponse, operationName)));
                }
            }
            return null;
        }

        private static bool HasStream(MessageDescription messageDescription)
        {
            if (messageDescription.Body.ReturnValue != null && messageDescription.Body.ReturnValue.Type == typeof(Stream))
            {
                return true;
            }

            foreach (MessagePartDescription part in messageDescription.Body.Parts)
            {
                if (part.Type == typeof(Stream))
                {
                    return true;
                }
            }
            return false;
        }

        private static MessagePartDescription GetStreamPart(MessageDescription messageDescription)
        {
            if (OperationFormatter.IsValidReturnValue(messageDescription.Body.ReturnValue))
            {
                if (messageDescription.Body.Parts.Count == 0)
                {
                    if (messageDescription.Body.ReturnValue.Type == typeof(Stream))
                    {
                        return messageDescription.Body.ReturnValue;
                    }
                }
            }
            else
            {
                if (messageDescription.Body.Parts.Count == 1)
                {
                    if (messageDescription.Body.Parts[0].Type == typeof(Stream))
                    {
                        return messageDescription.Body.Parts[0];
                    }
                }
            }
            return null;
        }

        internal static bool IsStream(MessageDescription messageDescription)
        {
            return GetStreamPart(messageDescription) != null;
        }

        internal class MessageBodyStream : Stream
        {
            private readonly Message message;
            private XmlDictionaryReader reader;
            private long position;
            private readonly string wrapperName, wrapperNs;
            private readonly string elementName, elementNs;
            private readonly bool isRequest;
            internal MessageBodyStream(Message message, string wrapperName, string wrapperNs, string elementName, string elementNs, bool isRequest)
            {
                this.message = message;
                position = 0;
                this.wrapperName = wrapperName;
                this.wrapperNs = wrapperNs;
                this.elementName = elementName;
                this.elementNs = elementNs;
                this.isRequest = isRequest;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                EnsureStreamIsOpen();
                if (buffer == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(buffer)), message);
                }

                if (offset < 0)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), offset,
                                                    SR.ValueMustBeNonNegative), message);
                }

                if (count < 0)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), count,
                                                    SR.ValueMustBeNonNegative), message);
                }

                if (buffer.Length - offset < count)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SFxInvalidStreamOffsetLength, offset + count)), message);
                }

                try
                {

                    if (reader == null)
                    {
                        reader = message.GetReaderAtBodyContents();
                        if (wrapperName != null)
                        {
                            reader.MoveToContent();
                            reader.ReadStartElement(wrapperName, wrapperNs);
                        }
                        reader.MoveToContent();
                        if (reader.NodeType == XmlNodeType.EndElement)
                        {
                            return 0;
                        }

                        reader.ReadStartElement(elementName, elementNs);
                    }
                    if (reader.MoveToContent() != XmlNodeType.Text)
                    {
                        Exhaust(reader);
                        return 0;
                    }
                    int bytesRead = reader.ReadContentAsBase64(buffer, offset, count);
                    position += bytesRead;
                    if (bytesRead == 0)
                    {
                        Exhaust(reader);
                    }
                    return bytesRead;
                }
                catch (Exception ex)
                {
                    if (Fx.IsFatal(ex))
                    {
                        throw;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new IOException(SR.SFxStreamIOException, ex));
                }
            }

            private void EnsureStreamIsOpen()
            {
                if (message.State == MessageState.Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(
                        isRequest ? SR.SFxStreamRequestMessageClosed : SR.SFxStreamResponseMessageClosed));
                }
            }

            private static void Exhaust(XmlDictionaryReader reader)
            {
                if (reader != null)
                {
                    while (reader.Read())
                    {
                        // drain
                    }
                }
            }

            public override long Position
            {
                get
                {
                    EnsureStreamIsOpen();
                    return position;
                }
                set { throw TraceUtility.ThrowHelperError(new NotSupportedException(), message); }
            }

            protected override void Dispose(bool isDisposing)
            {
                message.Close();
                if (reader != null)
                {
                    reader.Dispose();
                    reader = null;
                }
                base.Dispose(isDisposing);
            }

            public override bool CanRead { get { return message.State != MessageState.Closed; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length
            {
                get
                {
                    throw TraceUtility.ThrowHelperError(new NotSupportedException(), message);
                }
            }
            public override void Flush() { throw TraceUtility.ThrowHelperError(new NotSupportedException(), message); }
            public override long Seek(long offset, SeekOrigin origin) { throw TraceUtility.ThrowHelperError(new NotSupportedException(), message); }
            public override void SetLength(long value) { throw TraceUtility.ThrowHelperError(new NotSupportedException(), message); }
            public override void Write(byte[] buffer, int offset, int count) { throw TraceUtility.ThrowHelperError(new NotSupportedException(), message); }
        }

        internal class OperationStreamProvider //: IStreamProvider
        {
            private readonly Stream stream;

            internal OperationStreamProvider(Stream stream)
            {
                this.stream = stream;
            }

            public Stream GetStream()
            {
                return stream;
            }
            public void ReleaseStream(Stream stream)
            {
                //Noop
            }
        }

        internal class StreamFormatterHelper
        {
            // The method was duplicated from the desktop implementation of
            // System.Xml.XmlDictionaryWriter.WriteValue(IStreamProvider)
            public static void WriteValue(XmlDictionaryWriter writer, OperationStreamProvider value)
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                Stream stream = value.GetStream();
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.XmlInvalidStream)));
                }

                int blockSize = 256;
                int bytesRead = 0;
                byte[] block = new byte[blockSize];
                while (true)
                {
                    bytesRead = stream.Read(block, 0, blockSize);
                    if (bytesRead > 0)
                    {
                        writer.WriteBase64(block, 0, bytesRead);
                    }
                    else
                    {
                        break;
                    }

                    if (blockSize < 65536 && bytesRead == blockSize)
                    {
                        blockSize = blockSize * 16;
                        block = new byte[blockSize];
                    }
                }

                value.ReleaseStream(stream);
            }

            internal static async Task WriteValueAsync(XmlDictionaryWriter writer, OperationStreamProvider value)
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
                }

                Stream stream = value.GetStream();
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.XmlInvalidStream));
                }

                int blockSize = 256;
                int bytesRead = 0;
                byte[] block = new byte[blockSize];
                while (true)
                {
                    bytesRead = await stream.ReadAsync(block, 0, blockSize);
                    if (bytesRead > 0)
                    {
                        // XmlDictionaryWriter has not implemented WriteBase64Async() yet.
                        writer.WriteBase64(block, 0, bytesRead);
                    }
                    else
                    {
                        break;
                    }

                    if (blockSize < 65536 && bytesRead == blockSize)
                    {
                        blockSize = blockSize * 16;
                        block = new byte[blockSize];
                    }
                }

                value.ReleaseStream(stream);
            }
        }

    }

}