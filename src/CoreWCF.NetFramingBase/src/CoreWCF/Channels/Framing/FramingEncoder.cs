// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                    SRCommon.ValueMustBeNonNegative));
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

    internal abstract class EncodedFramingRecord
    {
        protected EncodedFramingRecord(byte[] encodedBytes)
        {
            EncodedBytes = encodedBytes;
        }

        internal EncodedFramingRecord(FramingRecordType recordType, string value)
        {
            int valueByteCount = Encoding.UTF8.GetByteCount(value);
            int sizeByteCount = IntEncoder.GetEncodedSize(valueByteCount);
            EncodedBytes = Fx.AllocateByteArray(checked(1 + sizeByteCount + valueByteCount));
            EncodedBytes[0] = (byte)recordType;
            int offset = 1;
            offset += IntEncoder.Encode(valueByteCount, EncodedBytes, offset);
            Encoding.UTF8.GetBytes(value, 0, value.Length, EncodedBytes, offset);
            SetEncodedBytes(EncodedBytes);
        }


        public byte[] EncodedBytes { get; private set; }

        protected void SetEncodedBytes(byte[] encodedBytes)
        {
            EncodedBytes = encodedBytes;
        }

        public override int GetHashCode()
        {
            return (EncodedBytes[0] << 16) |
                (EncodedBytes[EncodedBytes.Length / 2] << 8) |
                EncodedBytes[EncodedBytes.Length - 1];
        }

        public override bool Equals(object o)
        {
            if (o is EncodedFramingRecord)
            {
                return Equals((EncodedFramingRecord)o);
            }

            return false;
        }

        public bool Equals(EncodedFramingRecord other)
        {
            if (other == null)
            {
                return false;
            }

            if (other == this)
            {
                return true;
            }

            byte[] otherBytes = other.EncodedBytes;
            if (EncodedBytes.Length != otherBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < EncodedBytes.Length; i++)
            {
                if (EncodedBytes[i] != otherBytes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal class EncodedContentType : EncodedFramingRecord
    {
        private EncodedContentType(FramingEncodingType encodingType) :
            base(new byte[] { (byte)FramingRecordType.KnownEncoding, (byte)encodingType })
        {
        }

        private EncodedContentType(string contentType)
            : base(FramingRecordType.ExtensibleEncoding, contentType)
        {
        }

        public static EncodedContentType Create(string contentType)
        {
            if (contentType == FramingEncodingString.ExtendedBinarySessionDeflate)
            {
                return new EncodedContentType(FramingEncodingType.ExtendedBinarySessionDeflate);
            }
            else if (contentType == FramingEncodingString.ExtendedBinaryDeflate)
            {
                return new EncodedContentType(FramingEncodingType.ExtendedBinaryDeflate);
            }
            else if (contentType == FramingEncodingString.ExtendedBinarySessionGZip)
            {
                return new EncodedContentType(FramingEncodingType.ExtendedBinarySessionGZip);
            }
            else if (contentType == FramingEncodingString.ExtendedBinaryGZip)
            {
                return new EncodedContentType(FramingEncodingType.ExtendedBinaryGZip);
            }
            else if(contentType == FramingEncodingString.BinarySession)
            {
                return new EncodedContentType(FramingEncodingType.BinarySession);
            }
            else if (contentType == FramingEncodingString.Binary)
            {
                return new EncodedContentType(FramingEncodingType.Binary);
            }
            else if (contentType == FramingEncodingString.Soap12Utf8)
            {
                return new EncodedContentType(FramingEncodingType.Soap12Utf8);
            }
            else if (contentType == FramingEncodingString.Soap11Utf8)
            {
                return new EncodedContentType(FramingEncodingType.Soap11Utf8);
            }
            else if (contentType == FramingEncodingString.Soap12Utf16)
            {
                return new EncodedContentType(FramingEncodingType.Soap12Utf16);
            }
            else if (contentType == FramingEncodingString.Soap11Utf16)
            {
                return new EncodedContentType(FramingEncodingType.Soap11Utf16);
            }
            else if (contentType == FramingEncodingString.Soap12Utf16FFFE)
            {
                return new EncodedContentType(FramingEncodingType.Soap12Utf16FFFE);
            }
            else if (contentType == FramingEncodingString.Soap11Utf16FFFE)
            {
                return new EncodedContentType(FramingEncodingType.Soap11Utf16FFFE);
            }
            else if (contentType == FramingEncodingString.MTOM)
            {
                return new EncodedContentType(FramingEncodingType.MTOM);
            }
            else
            {
                return new EncodedContentType(contentType);
            }
        }
    }

    internal class EncodedVia : EncodedFramingRecord
    {
        public EncodedVia(string via)
            : base(FramingRecordType.Via, via)
        {
        }
    }

    internal class EncodedUpgrade : EncodedFramingRecord
    {
        public EncodedUpgrade(string contentType)
            : base(FramingRecordType.UpgradeRequest, contentType)
        {
        }
    }

    internal class EncodedFault : EncodedFramingRecord
    {
        public EncodedFault(string fault)
            : base(FramingRecordType.Fault, fault)
        {
        }
    }

    // used by SimplexEncoder/DuplexEncoder
    internal abstract class SessionEncoder
    {
        public const int MaxMessageFrameSize = 1 + IntEncoder.MaxEncodedSize;

        protected SessionEncoder() { }

        public static byte[] PreambleEndBytes = new byte[] {
            (byte)FramingRecordType.PreambleEnd
        };

        public static byte[] EndBytes = new byte[] {
            (byte)FramingRecordType.End
        };

        public static int CalcStartSize(EncodedVia via, EncodedContentType contentType)
        {
            return via.EncodedBytes.Length + contentType.EncodedBytes.Length;
        }

        public static void EncodeStart(byte[] buffer, int offset, EncodedVia via, EncodedContentType contentType)
        {
            Buffer.BlockCopy(via.EncodedBytes, 0, buffer, offset, via.EncodedBytes.Length);
            Buffer.BlockCopy(contentType.EncodedBytes, 0, buffer, offset + via.EncodedBytes.Length, contentType.EncodedBytes.Length);
        }

        public static ArraySegment<byte> EncodeMessageFrame(ArraySegment<byte> messageFrame)
        {
            int spaceNeeded = 1 + IntEncoder.GetEncodedSize(messageFrame.Count);
            int offset = messageFrame.Offset - spaceNeeded;
            if (offset < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("messageFrame.Offset",
                    messageFrame.Offset, SR.Format(SR.SpaceNeededExceedsMessageFrameOffset, spaceNeeded)));
            }

            byte[] buffer = messageFrame.Array;
            buffer[offset++] = (byte)FramingRecordType.SizedEnvelope;
            IntEncoder.Encode(messageFrame.Count, buffer, offset);
            return new ArraySegment<byte>(buffer, messageFrame.Offset - spaceNeeded, messageFrame.Count + spaceNeeded);
        }
    }

    // used by ServerDuplexEncoder/ServerSimplexEncoder
    internal abstract class ServerSessionEncoder : SessionEncoder
    {
        protected ServerSessionEncoder() { }

        public static byte[] AckResponseBytes = new byte[] {
            (byte)FramingRecordType.PreambleAck
        };
        public static byte[] UpgradeResponseBytes = new byte[] {
            (byte)FramingRecordType.UpgradeResponse
        };
    }

    // Pattern: 
    //   ModeBytes, 
    //   EncodeStart, 
    //   EncodeUpgrade*, 
    //   EncodeMessageFrame*, 
    //   EndBytes
    internal class ClientDuplexEncoder : SessionEncoder
    {
        private ClientDuplexEncoder() { }

        public static byte[] ModeBytes = new byte[] {
            (byte)FramingRecordType.Version,
            (byte)FramingVersion.Major,
            (byte)FramingVersion.Minor,
            (byte)FramingRecordType.Mode,
            (byte)FramingMode.Duplex };
    }

    // Pattern: 
    //   ModeBytes, 
    //   EncodeStart, 
    //   EncodeUpgrade*, 
    //   EncodeMessageFrame*, 
    //   EndBytes
    internal class ClientSimplexEncoder : SessionEncoder
    {
        private ClientSimplexEncoder() { }

        public static byte[] ModeBytes = new byte[] {
            (byte)FramingRecordType.Version,
            (byte)FramingVersion.Major,
            (byte)FramingVersion.Minor,
            (byte)FramingRecordType.Mode,
            (byte)FramingMode.Simplex };
    }

    // shared code for client and server
    internal abstract class SingletonEncoder
    {
        protected SingletonEncoder()
        {
        }

        public static byte[] EnvelopeStartBytes = new byte[] {
            (byte)FramingRecordType.UnsizedEnvelope };

        public static byte[] EnvelopeEndBytes = new byte[] {
            (byte)0 };

        public static byte[] EnvelopeEndFramingEndBytes = new byte[] {
            (byte)0, (byte)FramingRecordType.End };

        public static byte[] EndBytes = new byte[] {
            (byte)FramingRecordType.End };

        public static ArraySegment<byte> EncodeMessageFrame(ArraySegment<byte> messageFrame)
        {
            int spaceNeeded = IntEncoder.GetEncodedSize(messageFrame.Count);
            int offset = messageFrame.Offset - spaceNeeded;
            if (offset < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("messageFrame.Offset",
                    messageFrame.Offset, SR.Format(SR.SpaceNeededExceedsMessageFrameOffset, spaceNeeded)));
            }

            byte[] buffer = messageFrame.Array;
            IntEncoder.Encode(messageFrame.Count, buffer, offset);
            return new ArraySegment<byte>(buffer, offset, messageFrame.Count + spaceNeeded);
        }
    }

    // Pattern: 
    //   ModeBytes, 
    //   EncodeStart, 
    //   EncodeUpgrade*, 
    //   EnvelopeStartBytes,
    //   streamed-message-bytes*
    internal class ClientSingletonEncoder : SingletonEncoder
    {
        private ClientSingletonEncoder() { }


        public static byte[] PreambleEndBytes = new byte[] {
            (byte)FramingRecordType.PreambleEnd
        };

        public static byte[] ModeBytes = new byte[] {
            (byte)FramingRecordType.Version,
            (byte)FramingVersion.Major,
            (byte)FramingVersion.Minor,
            (byte)FramingRecordType.Mode,
            (byte)FramingMode.Singleton };

        public static int CalcStartSize(EncodedVia via, EncodedContentType contentType)
        {
            return via.EncodedBytes.Length + contentType.EncodedBytes.Length;
        }

        public static void EncodeStart(byte[] buffer, int offset, EncodedVia via, EncodedContentType contentType)
        {
            Buffer.BlockCopy(via.EncodedBytes, 0, buffer, offset, via.EncodedBytes.Length);
            Buffer.BlockCopy(contentType.EncodedBytes, 0, buffer, offset + via.EncodedBytes.Length, contentType.EncodedBytes.Length);
        }
    }

    // Pattern:
    //   (UpgradeResponseBytes, upgrade-bytes)?, 
    internal class ServerSingletonEncoder : SingletonEncoder
    {
        private ServerSingletonEncoder() { }

        public static byte[] AckResponseBytes = new byte[] {
            (byte)FramingRecordType.PreambleAck
        };

        public static byte[] UpgradeResponseBytes = new byte[] {
            (byte)FramingRecordType.UpgradeResponse
        };
    }
}
