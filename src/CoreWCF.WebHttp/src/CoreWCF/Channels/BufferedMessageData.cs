// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class BufferedMessageData : IBufferedMessageData2
    {
        private ReadOnlySequence<byte> _readOnlyBuffer;
        private int _refCount;
        private int _outstandingReaders;
        private bool _multipleUsers;
        private RecycledMessageState _messageState;
        private readonly SynchronizedPool<RecycledMessageState> _messageStatePool;

        public BufferedMessageData(SynchronizedPool<RecycledMessageState> messageStatePool)
        {
            _messageStatePool = messageStatePool;
        }

        public ReadOnlySequence<byte> ReadOnlyBuffer => _readOnlyBuffer;
        public virtual XmlDictionaryReaderQuotas Quotas => XmlDictionaryReaderQuotas.Max;

        public abstract MessageEncoder MessageEncoder { get; }

        public ArraySegment<byte> Buffer => throw new NotSupportedException();

        private object ThisLock => this;

        public void EnableMultipleUsers()
        {
            _multipleUsers = true;
        }

        public void Close()
        {
            if (_multipleUsers)
            {
                lock (ThisLock)
                {
                    if (--_refCount == 0)
                    {
                        DoClose();
                    }
                }
            }
            else
            {
                DoClose();
            }
        }

        private void DoClose()
        {
            if (_outstandingReaders == 0)
            {
                OnClosed();
            }
        }

        public void DoReturnMessageState(RecycledMessageState messageState)
        {
            if (_messageState == null)
            {
                _messageState = messageState;
            }
            else
            {
                _messageStatePool.Return(messageState);
            }
        }

        private void DoReturnXmlReader(XmlDictionaryReader reader)
        {
            ReturnXmlReader(reader);
            _outstandingReaders--;
        }

        public RecycledMessageState DoTakeMessageState()
        {
            RecycledMessageState messageState = _messageState;
            if (messageState != null)
            {
                _messageState = null;
                return messageState;
            }
            else
            {
                return _messageStatePool.Take();
            }
        }

        private XmlDictionaryReader DoTakeXmlReader()
        {
            XmlDictionaryReader reader = TakeXmlReader();
            _outstandingReaders++;

            return reader;
        }

        public XmlDictionaryReader GetMessageReader()
        {
            if (_multipleUsers)
            {
                lock (ThisLock)
                {
                    return DoTakeXmlReader();
                }
            }
            else
            {
                return DoTakeXmlReader();
            }
        }

        public void OnXmlReaderClosed(XmlDictionaryReader reader)
        {
            if (_multipleUsers)
            {
                lock (ThisLock)
                {
                    DoReturnXmlReader(reader);
                }
            }
            else
            {
                DoReturnXmlReader(reader);
            }
        }

        protected virtual void OnClosed()
        {
        }

        public RecycledMessageState TakeMessageState()
        {
            if (_multipleUsers)
            {
                lock (ThisLock)
                {
                    return DoTakeMessageState();
                }
            }
            else
            {
                return DoTakeMessageState();
            }
        }

        protected abstract XmlDictionaryReader TakeXmlReader();

        public void Open()
        {
            lock (ThisLock)
            {
                _refCount++;
            }
        }

        public void Open(ReadOnlySequence<byte> buffer)
        {
            _refCount = 1;
            _readOnlyBuffer = buffer;
            _multipleUsers = false;
        }

        protected abstract void ReturnXmlReader(XmlDictionaryReader xmlReader);

        public void ReturnMessageState(RecycledMessageState messageState)
        {
            if (_multipleUsers)
            {
                lock (ThisLock)
                {
                    DoReturnMessageState(messageState);
                }
            }
            else
            {
                DoReturnMessageState(messageState);
            }
        }
    }
}
