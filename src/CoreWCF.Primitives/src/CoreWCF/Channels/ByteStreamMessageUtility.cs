// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal static class ByteStreamMessageUtility
    {
        public const string StreamElementName = "Binary";
        public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
        public const string XmlNamespaceNamespace = "http://www.w3.org/2000/xmlns/";

        // used when doing message tracing
        internal const string EncoderName = "ByteStreamMessageEncoder";

        internal static void EnsureByteBoundaries(byte[] buffer, int index, int count, bool isRead)
        {
            if (buffer == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(buffer));
            }

            if (index < 0)
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(index), index, SR.Format(SR.ArgumentOutOfMinRange, 0));
            }

            // we explicitly allow the case for index = 0, buffer.Length = 0 and count = 0 when it is write
            // Note that we rely on the last check of count > buffer.Length - index to cover count > 0 && index == buffer.Length case 
            if (index > buffer.Length || (isRead && index == buffer.Length))
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(index), index, SR.Format(SR.OffsetExceedsBufferSize, buffer.Length));
            }

            if (count < 0)
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(count), count, SR.Format(SR.ArgumentOutOfMinRange, 0));
            }

            if (count > buffer.Length - index)
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(count), count, SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - index));
            }
        }

        internal static XmlDictionaryReaderQuotas EnsureQuotas(XmlDictionaryReaderQuotas quotas) => quotas ?? EncoderDefaults.ReaderQuotas;
    }
}
