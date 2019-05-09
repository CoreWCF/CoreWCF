using System;
using System.Text;
using Microsoft.Runtime;

namespace Microsoft.ServiceModel.Channels
{
    internal static class IntEncoder
    {
        public const int MaxEncodedSize = 5;

        public static int Encode(int value, byte[] bytes, int offset)
        {
            int count = 1;
            while ((value & 0xFFFFFF80) != 0)
            {
                bytes[offset++] = (byte)((value & 0x7F) | 0x80);
                count++;
                value >>= 7;
            }
            bytes[offset] = (byte)value;
            return count;
        }

        public static int GetEncodedSize(int value)
        {
            if (value < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                    SR.ValueMustBeNonNegative));
            }

            int count = 1;
            while ((value & 0xFFFFFF80) != 0)
            {
                count++;
                value >>= 7;
            }
            return count;
        }
    }
}