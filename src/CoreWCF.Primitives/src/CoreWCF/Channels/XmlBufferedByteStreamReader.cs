// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

            _offset = bufferedMessageData.Buffer.Offset;
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

            int bytesToCopy = Math.Min(_bufferedMessageData.Buffer.Count - _offset, count);

            if (bytesToCopy == 0)
            {
                position = ReaderPosition.EndElement;
                return 0;
            }

            Buffer.BlockCopy(_bufferedMessageData.Buffer.Array, _offset, buffer, index, bytesToCopy);
            _offset += bytesToCopy;

            return bytesToCopy;
        }

        protected override byte[] OnToByteArray()
        {
            int bytesToCopy = _bufferedMessageData.Buffer.Count;
            byte[] buffer = new byte[bytesToCopy];
            Buffer.BlockCopy(_bufferedMessageData.Buffer.Array, _bufferedMessageData.Buffer.Offset, buffer, 0, bytesToCopy);
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
                length = _bufferedMessageData.Buffer.Count;
                return true;
            }
            length = -1;
            return false;
        }
    }
}
