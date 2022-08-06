// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

namespace CoreWCF.Channels
{
    internal abstract class StringDecoder
    {
        private int _encodedSize;
        private byte[] _encodedBytes;
        private int _bytesNeeded;
        private string _value;
        private State _currentState;
        private IntDecoder _sizeDecoder;
        private readonly int _sizeQuota;
        private int _valueLengthInBytes;

        public StringDecoder(int sizeQuota)
        {
            _sizeQuota = sizeQuota;
            _sizeDecoder = new IntDecoder();
            _currentState = State.ReadingSize;
            Reset();
        }

        public bool IsValueDecoded
        {
            get { return _currentState == State.Done; }
        }

        public string Value
        {
            get
            {
                if (_currentState != State.Done)
                    throw new InvalidOperationException(SR.FramingValueNotAvailable);
                return _value;
            }
        }

        public int Decode(byte[] buffer, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);

            int bytesConsumed;
            switch (_currentState)
            {
                case State.ReadingSize:
                    bytesConsumed = _sizeDecoder.Decode(buffer, offset, size);
                    if (_sizeDecoder.IsValueDecoded)
                    {
                        _encodedSize = _sizeDecoder.Value;
                        if (_encodedSize > _sizeQuota)
                        {
                            Exception quotaExceeded = OnSizeQuotaExceeded(_encodedSize);
                            throw quotaExceeded;
                        }
                        if (_encodedBytes == null || _encodedBytes.Length < _encodedSize)
                        {
                            _encodedBytes = new byte[_encodedSize];
                            _value = null;
                        }
                        _currentState = State.ReadingBytes;
                        _bytesNeeded = _encodedSize;
                    }
                    break;
                case State.ReadingBytes:
                    if (_value != null && _valueLengthInBytes == _encodedSize && _bytesNeeded == _encodedSize &&
                        size >= _encodedSize && CompareBuffers(_encodedBytes, buffer, offset))
                    {
                        bytesConsumed = _bytesNeeded;
                        OnComplete(_value);
                    }
                    else
                    {
                        bytesConsumed = _bytesNeeded;
                        if (size < _bytesNeeded)
                            bytesConsumed = size;
                        Buffer.BlockCopy(buffer, offset, _encodedBytes, _encodedSize - _bytesNeeded, bytesConsumed);
                        _bytesNeeded -= bytesConsumed;
                        if (_bytesNeeded == 0)
                        {
                            _value = Encoding.UTF8.GetString(_encodedBytes, 0, _encodedSize);
                            _valueLengthInBytes = _encodedSize;
                            OnComplete(_value);
                        }
                    }
                    break;
                default:
                    throw new InvalidDataException(SR.InvalidDecoderStateMachine);
            }

            return bytesConsumed;
        }

        protected virtual void OnComplete(string value)
        {
            _currentState = State.Done;
        }

        private static bool CompareBuffers(byte[] buffer1, byte[] buffer2, int offset)
        {
            for (int i = 0; i < buffer1.Length; i++)
            {
                if (buffer1[i] != buffer2[i + offset])
                {
                    return false;
                }
            }
            return true;
        }

        protected abstract Exception OnSizeQuotaExceeded(int size);

        public void Reset()
        {
            _currentState = State.ReadingSize;
            _sizeDecoder.Reset();
        }

        private enum State
        {
            ReadingSize,
            ReadingBytes,
            Done,
        }
    }
}
