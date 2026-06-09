// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;

namespace CoreWCF.Channels
{
    internal static class DecoderHelper
    {
        public static void ValidateSize(int size)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(size), size, SRCommon.ValueMustBePositive));
            }
        }
    }

    internal struct IntDecoder
    {
        private int _value;
        private short _index;
        private const int LastIndex = 4;

        public int Value
        {
            get
            {
                if (!IsValueDecoded)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _value;
            }
        }

        public bool IsValueDecoded { get; private set; }

        public void Reset()
        {
            _index = 0;
            _value = 0;
            IsValueDecoded = false;
        }

        public int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize((int)buffer.Length);
            if (IsValueDecoded)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
            }

            int bytesConsumed = 0;

            while (bytesConsumed < buffer.Length)
            {
                ReadOnlySpan<byte> data = buffer.First.Span;
                int next = data[0];
                _value |= (next & 0x7F) << (_index * 7);
                bytesConsumed++;
                if (_index == LastIndex && (next & 0xF8) != 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.FramingSizeTooLarge));
                }
                _index++;
                if ((next & 0x80) == 0)
                {
                    IsValueDecoded = true;
                    break;
                }
                buffer = buffer.Slice(buffer.GetPosition(1));
            }

            return bytesConsumed;
        }
    }
}
