// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class BufferedMessageData : IBufferedMessageData2
    {
        private ReadOnlySequence<byte> _readOnlyBuffer;
        private BufferManager _bufferManager;
        private ReadOnlySequence<byte> _rentedBuffer;
        private int _refCount;
        private int _outstandingReaders;
        private bool _closePending;
        private bool _multipleUsers;
        private RecycledMessageState _messageState;
        private readonly SynchronizedPool<RecycledMessageState> _messageStatePool;

        public BufferedMessageData(SynchronizedPool<RecycledMessageState> messageStatePool)
        {
            _messageStatePool = messageStatePool;
        }

        [Obsolete]
        public ArraySegment<byte> Buffer
            => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                new NotSupportedException(SR.BufferedMessageDataBufferNotSupported));

        public ReadOnlySequence<byte> ReadOnlyBuffer => _readOnlyBuffer;

        public virtual XmlDictionaryReaderQuotas Quotas
        {
            get { return XmlDictionaryReaderQuotas.Max; }
        }

        public abstract MessageEncoder MessageEncoder { get; }

        private object ThisLock
        {
            get { return this; }
        }

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
                Release();
            }
            else
            {
                // A body reader is still out, so the buffer can't go back yet without being
                // recycled underneath it. DoReturnXmlReader finishes the job when the last one
                // closes; without this the buffer and the pooled message data are stranded.
                _closePending = true;
            }
        }

        private void Release()
        {
            ReturnRentedBuffer();
            _readOnlyBuffer = default;
            OnClosed();
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

            if (_closePending && _outstandingReaders == 0)
            {
                _closePending = false;
                Release();
            }
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

        /// <summary>
        /// Hands ownership of <paramref name="rentedBuffer"/> to this instance: the array behind it
        /// goes back to <paramref name="bufferManager"/> once the message closes. Callers reading
        /// out of memory they don't own - a PipeReader's own buffers, for instance - never call
        /// this, and the sequence the message reads from may differ from the one owned here.
        /// </summary>
        public void OwnBuffer(ReadOnlySequence<byte> rentedBuffer, BufferManager bufferManager)
        {
            if (bufferManager != null && !rentedBuffer.IsEmpty)
            {
                _rentedBuffer = rentedBuffer;
                _bufferManager = bufferManager;
            }
        }

        private void ReturnRentedBuffer()
        {
            if (_bufferManager == null)
            {
                return;
            }

            // One array per segment: a message received in pieces is chained rather than copied
            // into a single buffer, and every piece came from the manager.
            foreach (ReadOnlyMemory<byte> memory in _rentedBuffer)
            {
                if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment) && segment.Array != null)
                {
                    _bufferManager.ReturnBuffer(segment.Array);
                }
            }

            _bufferManager = null;
            _rentedBuffer = default;
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
