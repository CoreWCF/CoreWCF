// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace CoreWCF.Channels
{
    public sealed class UnderstoodHeaders : IEnumerable<MessageHeaderInfo>
    {
        private readonly MessageHeaders _messageHeaders;

        internal UnderstoodHeaders(MessageHeaders messageHeaders, bool modified)
        {
            _messageHeaders = messageHeaders;
            Modified = modified;
        }

        public bool Modified { get; set; }

        public void Add(MessageHeaderInfo headerInfo)
        {
            _messageHeaders.AddUnderstood(headerInfo);
            Modified = true;
        }

        public bool Contains(MessageHeaderInfo headerInfo)
        {
            return _messageHeaders.IsUnderstood(headerInfo);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<MessageHeaderInfo> GetEnumerator()
        {
            return _messageHeaders.GetUnderstoodEnumerator();
        }

        public void Remove(MessageHeaderInfo headerInfo)
        {
            _messageHeaders.RemoveUnderstood(headerInfo);
            Modified = true;
        }
    }
}
