// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;

namespace CoreWCF.Channels
{
    internal abstract class BufferedMessageWriter
    {
        private int[] _sizeHistory;
        private int _sizeHistoryIndex;
        private const int sizeHistoryCount = 4;
        private const int expectedSizeVariance = 256;
        private readonly BufferManagerOutputStream _stream;

        public BufferedMessageWriter()
        {
            _stream = new BufferManagerOutputStream(SRCommon.MaxSentMessageSizeExceeded);
            InitMessagePredictor();
        }

        protected abstract XmlDictionaryWriter TakeXmlWriter(Stream stream);
        protected abstract void ReturnXmlWriter(XmlDictionaryWriter writer);

        public ArraySegment<byte> WriteMessage(Message message, BufferManager bufferManager, int initialOffset, int maxSizeQuota)
        {
            int effectiveMaxSize;

            // make sure that maxSize has room for initialOffset without overflowing, since
            // the effective buffer size is message size + initialOffset
            if (maxSizeQuota <= int.MaxValue - initialOffset)
            {
                effectiveMaxSize = maxSizeQuota + initialOffset;
            }
            else
            {
                effectiveMaxSize = int.MaxValue;
            }

            int predictedMessageSize = PredictMessageSize();
            if (predictedMessageSize > effectiveMaxSize)
            {
                predictedMessageSize = effectiveMaxSize;
            }
            else if (predictedMessageSize < initialOffset)
            {
                predictedMessageSize = initialOffset;
            }

            try
            {
                _stream.Init(predictedMessageSize, maxSizeQuota, effectiveMaxSize, bufferManager);
                _stream.Skip(initialOffset);

                XmlDictionaryWriter writer = TakeXmlWriter(_stream);
                OnWriteStartMessage(writer);
                message.WriteMessage(writer);
                OnWriteEndMessage(writer);
                writer.Flush();
                ReturnXmlWriter(writer);
                byte[] buffer = _stream.ToArray(out int size);
                RecordActualMessageSize(size);
                return new ArraySegment<byte>(buffer, initialOffset, size - initialOffset);
            }
            finally
            {
                _stream.Clear();
            }
        }

        protected virtual void OnWriteStartMessage(XmlDictionaryWriter writer)
        {
        }

        protected virtual void OnWriteEndMessage(XmlDictionaryWriter writer)
        {
        }

        private void InitMessagePredictor()
        {
            _sizeHistory = new int[4];
            for (int i = 0; i < sizeHistoryCount; i++)
            {
                _sizeHistory[i] = 256;
            }
        }

        private int PredictMessageSize()
        {
            int max = 0;
            for (int i = 0; i < sizeHistoryCount; i++)
            {
                if (_sizeHistory[i] > max)
                {
                    max = _sizeHistory[i];
                }
            }

            return max + expectedSizeVariance;
        }

        private void RecordActualMessageSize(int size)
        {
            _sizeHistory[_sizeHistoryIndex] = size;
            _sizeHistoryIndex = (_sizeHistoryIndex + 1) % sizeHistoryCount;
        }
    }
}
