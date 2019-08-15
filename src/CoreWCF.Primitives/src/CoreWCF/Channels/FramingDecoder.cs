using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoreWCF.Channels
{
    static class DecoderHelper
    {
        public static void ValidateSize(int size)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(size), size, SR.ValueMustBePositive));
            }
        }
    }

    struct IntDecoder
    {
        int value;
        short index;
        bool isValueDecoded;
        const int LastIndex = 4;

        public int Value
        {
            get
            {
                if (!isValueDecoded)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                return value;
            }
        }

        public bool IsValueDecoded
        {
            get { return isValueDecoded; }
        }

        public void Reset()
        {
            index = 0;
            value = 0;
            isValueDecoded = false;
        }

        public int Decode(byte[] buffer, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);
            if (isValueDecoded)
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
                    isValueDecoded = true;
                    break;
                }
                offset++;
            }
            return bytesConsumed;
        }
    }

}
