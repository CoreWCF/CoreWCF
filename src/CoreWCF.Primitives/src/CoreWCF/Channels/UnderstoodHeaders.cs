// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace CoreWCF.Channels
{
    internal sealed class UnderstoodHeaders : IEnumerable<MessageHeaderInfo>
    {
        private readonly MessageHeaders messageHeaders;
        private bool modified;

        internal UnderstoodHeaders(MessageHeaders messageHeaders, bool modified)
        {
            this.messageHeaders = messageHeaders;
            this.modified = modified;
        }

        internal bool Modified
        {
            get { return modified; }
            set { modified = value; }
        }

        public void Add(MessageHeaderInfo headerInfo)
        {
            messageHeaders.AddUnderstood(headerInfo);
            modified = true;
        }

        public bool Contains(MessageHeaderInfo headerInfo)
        {
            return messageHeaders.IsUnderstood(headerInfo);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<MessageHeaderInfo> GetEnumerator()
        {
            return messageHeaders.GetUnderstoodEnumerator();
        }

        public void Remove(MessageHeaderInfo headerInfo)
        {
            messageHeaders.RemoveUnderstood(headerInfo);
            modified = true;
        }
    }
}