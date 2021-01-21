// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Channels
{
    internal static class DecoderHelper
    {
        public static void ValidateSize(int size)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(size), size, SR.ValueMustBePositive));
            }
        }
    }

    internal struct IntDecoder
    {
        private int value;
        private short index;
        private const int LastIndex = 4;

        public int Value
        {
            get
            {
                if (!IsValueDecoded)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return value;
            }
        }

        public bool IsValueDecoded { get; private set; }

        public void Reset()
        {
            index = 0;
            value = 0;
            IsValueDecoded = false;
        }

        public int Decode(byte[] buffer, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);
            if (IsValueDecoded)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
            }
            int bytesConsumed = 0;
            while (bytesConsumed < size)
            {
                int next = buffer[offset];
                value |= (next & 0x7F) << (index * 7);
                bytesConsumed++;
                if (index == LastIndex && (next & 0xF8) != 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.FramingSizeTooLarge));
                }
                index++;
                if ((next & 0x80) == 0)
                {
                    IsValueDecoded = true;
                    break;
                }
                offset++;
            }
            return bytesConsumed;
        }
    }

}
