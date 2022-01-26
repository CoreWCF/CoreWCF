// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal sealed class XmlByteStreamWriter : XmlDictionaryWriter
    {
        private readonly bool _ownsStream;
        private ByteStreamWriterState _state;
        private Stream _stream;
        private XmlWriterSettings _settings;

        public XmlByteStreamWriter(Stream stream, bool ownsStream)
        {
            Fx.Assert(stream != null, "stream is null");

            _stream = stream;
            _ownsStream = ownsStream;
            _state = ByteStreamWriterState.Start;
        }

        public override WriteState WriteState => ByteStreamWriterStateToWriteState(_state);

        public override XmlWriterSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    XmlWriterSettings newSettings = new XmlWriterSettings()
                    {
                        Async = true
                    };

                    Interlocked.CompareExchange(ref _settings, newSettings, null);
                }

                return _settings;
            }
        }

        public override void Close()
        {
            if (_state != ByteStreamWriterState.Closed)
            {
                try
                {
                    if (_ownsStream)
                    {
                        _stream.Close();
                    }
                    _stream = null;
                }
                finally
                {
                    _state = ByteStreamWriterState.Closed;
                }
            }
        }

        private void EnsureWriteBase64State(byte[] buffer, int index, int count)
        {
            ThrowIfClosed();
            ByteStreamMessageUtility.EnsureByteBoundaries(buffer, index, count, false);

            if (_state != ByteStreamWriterState.Content && _state != ByteStreamWriterState.StartElement)
            {
                throw Fx.Exception.AsError(
                    new InvalidOperationException(SR.Format(SR.XmlWriterMustBeInElement, ByteStreamWriterStateToWriteState(this._state))));
            }
        }

        public override void Flush()
        {
            ThrowIfClosed();
            _stream.Flush();
        }

        private void InternalWriteEndElement()
        {
            ThrowIfClosed();
            if (_state != ByteStreamWriterState.StartElement && _state != ByteStreamWriterState.Content)
            {
                throw Fx.Exception.AsError(
                    new InvalidOperationException(SR.XmlUnexpectedEndElement));
            }

            _state = ByteStreamWriterState.EndElement;
        }

        public override string LookupPrefix(string ns)
        {
            if (ns == string.Empty)
            {
                return string.Empty;
            }
            else if (ns == ByteStreamMessageUtility.XmlNamespace)
            {
                return "xml";
            }
            else if (ns == ByteStreamMessageUtility.XmlNamespaceNamespace)
            {
                return "xmlns";
            }
            else
            {
                return null;
            }
        }

        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            EnsureWriteBase64State(buffer, index, count);
            _stream.Write(buffer, index, count);
            _state = ByteStreamWriterState.Content;
        }

        public override async Task WriteBase64Async(byte[] buffer, int index, int count)
        {

            EnsureWriteBase64State(buffer, index, count);
            await _stream.WriteAsync(buffer, index, count);
            _state = ByteStreamWriterState.Content;
        }

        public override void WriteCData(string text) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteCharEntity(char ch) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteChars(char[] buffer, int index, int count) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteComment(string text) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteDocType(string name, string pubid, string sysid, string subset) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteEndAttribute() => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteEndDocument()
        {
        }

        public override void WriteEndElement()
        {
            InternalWriteEndElement();
        }

        public override void WriteEntityRef(string name) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteFullEndElement()
        {
            InternalWriteEndElement();
        }

        public override void WriteProcessingInstruction(string name, string text) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteRaw(string data) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteRaw(char[] buffer, int index, int count) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteStartAttribute(string prefix, string localName, string ns) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteStartDocument(bool standalone)
        {
            ThrowIfClosed();
        }

        public override void WriteStartDocument()
        {
            ThrowIfClosed();
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            ThrowIfClosed();
            if (_state != ByteStreamWriterState.Start)
            {
                throw Fx.Exception.AsError(
                    new InvalidOperationException(SR.ByteStreamWriteStartElementAlreadyCalled));
            }

            if (!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(ns) || localName != ByteStreamMessageUtility.StreamElementName)
            {
                throw Fx.Exception.AsError(
                    new XmlException(SR.Format(SR.XmlStartElementNameExpected, ByteStreamMessageUtility.StreamElementName, localName)));
            }

            _state = ByteStreamWriterState.StartElement;
        }

        public override void WriteString(string text)
        {
            // no state checks here - WriteBase64 will take care of this. 
            byte[] buffer = Convert.FromBase64String(text);
            WriteBase64(buffer, 0, buffer.Length);
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar) => throw Fx.Exception.AsError(new NotSupportedException());

        public override void WriteWhitespace(string ws) => throw Fx.Exception.AsError(new NotSupportedException());

        private void ThrowIfClosed()
        {
            if (_state == ByteStreamWriterState.Closed)
            {
                throw Fx.Exception.AsError(
                    new InvalidOperationException(SR.XmlWriterClosed));
            }
        }

        private static WriteState ByteStreamWriterStateToWriteState(ByteStreamWriterState byteStreamWriterState)
        {
            // Converts the internal ByteStreamWriterState to an Xml WriteState
            switch (byteStreamWriterState)
            {
                case ByteStreamWriterState.Start:
                    return WriteState.Start;
                case ByteStreamWriterState.StartElement:
                    return WriteState.Element;
                case ByteStreamWriterState.Content:
                    return WriteState.Content;
                case ByteStreamWriterState.EndElement:
                    return WriteState.Element;
                case ByteStreamWriterState.Closed:
                    return WriteState.Closed;
                default:
                    return WriteState.Error;
            }
        }

        private enum ByteStreamWriterState
        {
            Start,
            StartElement,
            Content,
            EndElement,
            Closed
        }
    }
}
