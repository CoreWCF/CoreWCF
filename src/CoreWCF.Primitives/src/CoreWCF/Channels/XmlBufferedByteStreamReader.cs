// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class XmlBufferedByteStreamReader : XmlByteStreamReader
    {
        private ByteStreamBufferedMessageData _bufferedMessageData;
        private int _offset;

        public XmlBufferedByteStreamReader(ByteStreamBufferedMessageData bufferedMessageData, XmlDictionaryReaderQuotas quotas) : base(quotas)
        {
            Fx.Assert(bufferedMessageData != null, "bufferedMessageData is null");
            _bufferedMessageData = bufferedMessageData;
            _bufferedMessageData.Open();

            _offset = 0;
            this.quotas = quotas;
            position = ReaderPosition.None;
        }

        protected override void OnClose()
        {
            _bufferedMessageData.Close();
            _bufferedMessageData = null;
            _offset = 0;
            base.OnClose();
        }

        public override int ReadContentAsBase64(byte[] buffer, int index, int count)
        {
            EnsureInContent();
            ByteStreamMessageUtility.EnsureByteBoundaries(buffer, index, count, true);

            if (count == 0)
            {
                return 0;
            }

            long remainingBytes = _bufferedMessageData.ReadOnlyBuffer.Length - _offset;
            if (remainingBytes <= 0)
            {
                position = ReaderPosition.EndElement;
                return 0;
            }

            int bytesToCopy = (int)Math.Min(remainingBytes, count);

            _bufferedMessageData.ReadOnlyBuffer.Slice(_offset, bytesToCopy).CopyTo(buffer.AsSpan(index, bytesToCopy));
            _offset += bytesToCopy;

            return bytesToCopy;
        }

        protected override byte[] OnToByteArray()
        {
            int bytesToCopy = (int)_bufferedMessageData.ReadOnlyBuffer.Length;
            byte[] buffer = new byte[bytesToCopy];
            _bufferedMessageData.ReadOnlyBuffer.CopyTo(buffer);
            return buffer;
        }

        protected override Stream OnToStream()
        {
            return _bufferedMessageData.ToStream();
        }

        public override bool TryGetBase64ContentLength(out int length)
        {
            if (!IsClosed)
            {
                // in ByteStream encoder, we're not concerned about individual xml nodes
                // therefore we can just return the entire segment of the buffer we're using in this reader.
                length = (int)_bufferedMessageData.ReadOnlyBuffer.Length;
                return true;
            }
            length = -1;
            return false;
        }
    }
}
