// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Channels
{
    internal abstract class FramingDecoder
    {
        protected FramingDecoder()
        {
        }

        protected FramingDecoder(long streamPosition)
        {
            StreamPosition = streamPosition;
        }

        protected abstract string CurrentStateAsString { get; }

        public long StreamPosition { get; set; }

        protected void ValidateFramingMode(FramingMode mode)
        {
            switch (mode)
            {
                case FramingMode.Singleton:
                case FramingMode.Duplex:
                case FramingMode.Simplex:
                case FramingMode.SingletonSized:
                    break;
                default:
                    {
                        throw new InvalidDataException(SR.Format(SR.FramingModeNotSupported, mode.ToString()));
                    }
            }
        }

        protected void ValidateRecordType(FramingRecordType expectedType, FramingRecordType foundType)
        {
            if (foundType != expectedType)
            {
                throw new InvalidDataException(SR.Format(SR.FramingRecordTypeMismatch, expectedType.ToString(), foundType.ToString()));
            }
        }



        protected void ValidateMajorVersion(int majorVersion)
        {
            if (majorVersion != FramingVersion.Major)
            {
                Exception exception = new InvalidDataException(SR.Format(SR.FramingVersionNotSupported, majorVersion));
                throw exception;
            }
        }

        public Exception CreatePrematureEOFException()
        {
            return CreateException(new InvalidDataException(SR.FramingPrematureEOF));
        }

        protected Exception CreateException(InvalidDataException innerException)
        {
            return new ProtocolException(SR.Format(SR.FramingError, StreamPosition, CurrentStateAsString),
                innerException);
        }
    }
}
