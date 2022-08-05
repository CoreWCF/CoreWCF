// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Channels
{
    internal class ServerModeDecoder : FramingDecoder
    {
        private int _majorVersion;
        private int _minorVersion;
        private FramingMode _mode;

        public ServerModeDecoder()
        {
            CurrentState = State.ReadingVersionRecord;
        }

        public int Decode(byte[] bytes, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);

            try
            {
                int bytesConsumed;
                switch (CurrentState)
                {
                    case State.ReadingVersionRecord:
                        ValidateRecordType(FramingRecordType.Version, (FramingRecordType)bytes[offset]);
                        CurrentState = State.ReadingMajorVersion;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingMajorVersion:
                        _majorVersion = bytes[offset];
                        ValidateMajorVersion(_majorVersion);
                        CurrentState = State.ReadingMinorVersion;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingMinorVersion:
                        _minorVersion = bytes[offset];
                        CurrentState = State.ReadingModeRecord;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingModeRecord:
                        ValidateRecordType(FramingRecordType.Mode, (FramingRecordType)bytes[offset]);
                        CurrentState = State.ReadingModeValue;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingModeValue:
                        _mode = (FramingMode)bytes[offset];
                        ValidateFramingMode(_mode);
                        CurrentState = State.Done;
                        bytesConsumed = 1;
                        break;
                    default:
                        throw new InvalidDataException(SR.InvalidDecoderStateMachine);
                }

                StreamPosition += bytesConsumed;
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw CreateException(e);
            }
        }

        public void Reset()
        {
            StreamPosition = 0;
            CurrentState = State.ReadingVersionRecord;
        }

        public State CurrentState { get; private set; }

        protected override string CurrentStateAsString
        {
            get { return CurrentState.ToString(); }
        }

        public FramingMode Mode
        {
            get
            {
                if (CurrentState != State.Done)
                    throw new InvalidOperationException(SR.FramingValueNotAvailable);

                return _mode;
            }
        }

        public int MajorVersion
        {
            get
            {
                if (CurrentState != State.Done)
                    throw new InvalidOperationException(SR.FramingValueNotAvailable);
                return _majorVersion;
            }
        }

        public int MinorVersion
        {
            get
            {
                if (CurrentState != State.Done)
                    throw new InvalidOperationException(SR.FramingValueNotAvailable);
                return _minorVersion;
            }
        }

        public enum State
        {
            ReadingVersionRecord,
            ReadingMajorVersion,
            ReadingMinorVersion,
            ReadingModeRecord,
            ReadingModeValue,
            Done,
        }
    }
}
