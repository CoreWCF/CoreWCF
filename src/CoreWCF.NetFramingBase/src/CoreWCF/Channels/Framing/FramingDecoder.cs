// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels.Framing
{
    internal static class DecoderHelper
    {
        public static void ValidateSize(long size)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(size), size, SRCommon.ValueMustBePositive));
            }
        }
    }

    internal struct IntDecoder
    {
        public IntDecoder(ILogger logger)
        {
            Logger = logger;
            IsValueDecoded = false;
            _value = 0;
            _index = 0;
        }

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

        private ILogger Logger { get; }

        public void Reset()
        {
            _index = 0;
            _value = 0;
            IsValueDecoded = false;
        }

        public int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);
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
                Logger.DecodingInt(next, _index,_value);
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

        public StringDecoder(int sizeQuota, ILogger logger)
        {
            Logger = logger;
            _sizeQuota = sizeQuota;
            _sizeDecoder = new IntDecoder(logger);
            Reset();
        }

        protected ILogger Logger { get; }

        public bool IsValueDecoded
        {
            get { return CurrentState == State.Done; }
        }

        public string Value
        {
            get
            {
                if (CurrentState != State.Done)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _value;
            }
        }

        public State CurrentState { get => _currentState; private set => _currentState = value; }

        public int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);

            int bytesConsumed;
            Logger.LogStartState(this);
            switch (CurrentState)
            {
                case State.ReadingSize:
                    bytesConsumed = _sizeDecoder.Decode(buffer);
                    if (_sizeDecoder.IsValueDecoded)
                    {
                        _encodedSize = _sizeDecoder.Value;
                        if (_encodedSize > _sizeQuota)
                        {
                            Exception quotaExceeded = OnSizeQuotaExceeded(_encodedSize);
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(quotaExceeded);
                        }
                        if (_encodedBytes == null || _encodedBytes.Length < _encodedSize)
                        {
                            _encodedBytes = Fx.AllocateByteArray(_encodedSize);
                            _value = null;
                        }
                        CurrentState = State.ReadingBytes;
                        _bytesNeeded = _encodedSize;
                    }
                    break;
                case State.ReadingBytes:
                    if (_value != null && _valueLengthInBytes == _encodedSize && _bytesNeeded == _encodedSize &&
                        buffer.Length >= _encodedSize && CompareBuffers(_encodedBytes, buffer))
                    {
                        bytesConsumed = _bytesNeeded;
                        OnComplete(_value);
                    }
                    else
                    {
                        bytesConsumed = _bytesNeeded;
                        if (buffer.Length < _bytesNeeded)
                        {
                            bytesConsumed = (int)buffer.Length;
                        }

                        Span<byte> span = _encodedBytes;
                        Span<byte> slicedBytes = span.Slice(_encodedSize - _bytesNeeded, bytesConsumed);
                        ReadOnlySequence<byte> tempBuffer = buffer.Slice(0, bytesConsumed);
                        tempBuffer.CopyTo(slicedBytes);
                        _bytesNeeded -= bytesConsumed;
                        if (_bytesNeeded == 0)
                        {
                            _value = Encoding.UTF8.GetString(_encodedBytes, 0, _encodedSize);
                            _valueLengthInBytes = _encodedSize;
                            Logger.StringDecoded(_value);
                            OnComplete(_value);
                        }
                    }
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.InvalidDecoderStateMachine));
            }

            Logger.LogEndState(this, bytesConsumed);
            return bytesConsumed;
        }

        protected virtual void OnComplete(string value)
        {
            CurrentState = State.Done;
        }

        private static bool CompareBuffers(byte[] buffer1, ReadOnlySequence<byte> buffer2)
        {
            byte[] buff = buffer2.ToArray();
            for (int i = 0; i < buffer1.Length; i++)
            {
                if (buffer1[i] != buff[i])
                {
                    return false;
                }
            }
            return true;
        }

        protected abstract Exception OnSizeQuotaExceeded(int size);

        public void Reset()
        {
            CurrentState = State.ReadingSize;
            _sizeDecoder.Reset();
        }

        public enum State
        {
            ReadingSize,
            ReadingBytes,
            Done,
        }
    }

    internal class ViaStringDecoder : StringDecoder
    {
        private Uri _via;

        public ViaStringDecoder(int sizeQuota, ILogger logger)
            : base(sizeQuota, logger)
        {
        }

        protected override Exception OnSizeQuotaExceeded(int size)
        {
            Exception result = new InvalidDataException(SR.Format(SR.FramingViaTooLong, size));
            FramingEncodingString.AddFaultString(result, FramingEncodingString.ViaTooLongFault);
            return result;
        }

        protected override void OnComplete(string value)
        {
            try
            {
                _via = new Uri(value);
                base.OnComplete(value);
            }
            catch (UriFormatException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.Format(SR.FramingViaNotUri, value), exception));
            }
        }

        public Uri ValueAsUri
        {
            get
            {
                if (!IsValueDecoded)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _via;
            }
        }
    }

    internal class FaultStringDecoder : StringDecoder
    {
        internal const int FaultSizeQuota = 256;

        public FaultStringDecoder(ILogger logger)
            : base(FaultSizeQuota, logger)
        {
        }

        protected override Exception OnSizeQuotaExceeded(int size)
        {
            return new InvalidDataException(SR.Format(SR.FramingFaultTooLong, size));
        }
    }

    internal class ContentTypeStringDecoder : StringDecoder
    {
        public ContentTypeStringDecoder(int sizeQuota, ILogger logger)
            : base(sizeQuota, logger)
        {
        }

        protected override Exception OnSizeQuotaExceeded(int size)
        {
            Exception result = new InvalidDataException(SR.Format(SR.FramingContentTypeTooLong, size));
            FramingEncodingString.AddFaultString(result, FramingEncodingString.ContentTypeTooLongFault);
            return result;
        }

        public static string GetString(FramingEncodingType type)
        {
            switch (type)
            {
                case FramingEncodingType.Soap11Utf8:
                    return FramingEncodingString.Soap11Utf8;
                case FramingEncodingType.Soap11Utf16:
                    return FramingEncodingString.Soap11Utf16;
                case FramingEncodingType.Soap11Utf16FFFE:
                    return FramingEncodingString.Soap11Utf16FFFE;
                case FramingEncodingType.Soap12Utf8:
                    return FramingEncodingString.Soap12Utf8;
                case FramingEncodingType.Soap12Utf16:
                    return FramingEncodingString.Soap12Utf16;
                case FramingEncodingType.Soap12Utf16FFFE:
                    return FramingEncodingString.Soap12Utf16FFFE;
                case FramingEncodingType.MTOM:
                    return FramingEncodingString.MTOM;
                case FramingEncodingType.Binary:
                    return FramingEncodingString.Binary;
                case FramingEncodingType.BinarySession:
                    return FramingEncodingString.BinarySession;
                case FramingEncodingType.ExtendedBinaryGZip:
                    return FramingEncodingString.ExtendedBinaryGZip;
                case FramingEncodingType.ExtendedBinarySessionGZip:
                    return FramingEncodingString.ExtendedBinarySessionGZip;
                case FramingEncodingType.ExtendedBinaryDeflate:
                    return FramingEncodingString.ExtendedBinaryDeflate;
                case FramingEncodingType.ExtendedBinarySessionDeflate:
                    return FramingEncodingString.ExtendedBinarySessionDeflate;
                default:
                    return "unknown" + ((int)type).ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    internal abstract class FramingDecoder
    {
        protected FramingDecoder(ILogger logger) => Logger = logger;

        protected abstract string CurrentStateAsString { get; }

        public virtual string ContentType { get { throw new NotImplementedException(); } }

        protected ILogger Logger { get; }

        public virtual Uri Via { get { throw new NotImplementedException(); } }

        public abstract int Decode(ReadOnlySequence<byte> buffer);

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
                        Exception exception = CreateException(new InvalidDataException(SR.Format(
                            SR.FramingModeNotSupported, mode.ToString())), FramingEncodingString.UnsupportedModeFault);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(exception);
                    }
            }
        }

        protected void ValidateRecordType(FramingRecordType expectedType, FramingRecordType foundType)
        {
            if (foundType != expectedType)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidRecordTypeException(expectedType, foundType));
            }
        }

        // special validation for Preamble Ack for usability purposes (MB#39593)
        protected void ValidatePreambleAck(FramingRecordType foundType)
        {
            if (foundType != FramingRecordType.PreambleAck)
            {
                Exception inner = CreateInvalidRecordTypeException(FramingRecordType.PreambleAck, foundType);
                string exceptionString;
                if (((byte)foundType == 'h') || ((byte)foundType == 'H'))
                {
                    exceptionString = SR.PreambleAckIncorrectMaybeHttp;
                }
                else
                {
                    exceptionString = SR.PreambleAckIncorrect;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(exceptionString, inner));
            }
        }

        private Exception CreateInvalidRecordTypeException(FramingRecordType expectedType, FramingRecordType foundType)
        {
            return new InvalidDataException(SR.Format(SR.FramingRecordTypeMismatch, expectedType.ToString(), foundType.ToString()));
        }

        protected void ValidateMajorVersion(int majorVersion)
        {
            if (majorVersion != FramingVersion.Major)
            {
                Exception exception = CreateException(new InvalidDataException(SR.Format(
                    SR.FramingVersionNotSupported, majorVersion)), FramingEncodingString.UnsupportedVersionFault);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(exception);
            }
        }

        public Exception CreatePrematureEOFException()
        {
            return CreateException(new InvalidDataException(SR.FramingPrematureEOF));
        }

        protected Exception CreateException(InvalidDataException innerException, string framingFault)
        {
            Exception result = CreateException(innerException);
            FramingEncodingString.AddFaultString(result, framingFault);
            return result;
        }

        protected Exception CreateException(InvalidDataException innerException)
        {
            // TODO: Can the position still be recovered?
            return new ProtocolException(SR.Format(SR.FramingError, /*StreamPosition*/ -1, CurrentStateAsString),
                innerException);
        }
    }

    // Pattern:
    //   Done
    internal class ServerModeDecoder : FramingDecoder
    {
        private int _majorVersion;
        private int _minorVersion;
        private FramingMode _mode;

        public ServerModeDecoder(ILogger logger) : base(logger)
        {
            Reset();
        }

        public override int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);
            ReadOnlySpan<byte> data = buffer.First.Span;

            try
            {
                int bytesConsumed;
                Logger.LogStartState(this);
                switch (CurrentState)
                {
                    case State.ReadingVersionRecord:
                        ValidateRecordType(FramingRecordType.Version, (FramingRecordType)data[0]);
                        CurrentState = State.ReadingMajorVersion;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingMajorVersion:
                        _majorVersion = data[0];
                        ValidateMajorVersion(_majorVersion);
                        CurrentState = State.ReadingMinorVersion;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingMinorVersion:
                        _minorVersion = data[0];
                        CurrentState = State.ReadingModeRecord;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingModeRecord:
                        ValidateRecordType(FramingRecordType.Mode, (FramingRecordType)data[0]);
                        CurrentState = State.ReadingModeValue;
                        bytesConsumed = 1;
                        break;
                    case State.ReadingModeValue:
                        _mode = (FramingMode)data[0];
                        ValidateFramingMode(_mode);
                        CurrentState = State.Done;
                        bytesConsumed = 1;
                        break;
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                Logger.LogEndState(this, bytesConsumed);
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }

        public void Reset()
        {
            CurrentState = State.ReadingVersionRecord;
        }

        internal async Task<bool> ReadModeAsync(PipeReader inputPipe, CancellationToken cancelToken)
        {
            ReadOnlySequence<byte> buffer;
            while (true)
            {
                ReadResult readResult = await inputPipe.ReadAsync(cancelToken);
                if (readResult.IsCompleted)
                {
                    return false;
                }

                buffer = readResult.Buffer;

                while (buffer.Length > 0)
                {
                    int bytesDecoded;
                    try
                    {
                        bytesDecoded = Decode(buffer);
                    }
                    catch (CommunicationException e)
                    {
                        // see if we need to send back a framing fault
                        if (FramingEncodingString.TryGetFaultString(e, out string framingFault))
                        {
                            // TODO: Drain the rest of the data and send a fault then close the connection
                            //byte[] drainBuffer = new byte[128];
                            //InitialServerConnectionReader.SendFault(
                            //    Connection, framingFault, drainBuffer, GetRemainingTimeout(),
                            //    MaxViaSize + MaxContentTypeSize);
                            //base.Close(GetRemainingTimeout());
                        }
                        throw;
                    }

                    if (bytesDecoded > 0)
                    {
                        buffer = buffer.Slice(bytesDecoded);
                    }

                    if (CurrentState == State.Done)
                    {
                        inputPipe.AdvanceTo(buffer.Start);
                        return true;
                    }
                }

                inputPipe.AdvanceTo(buffer.End);
            }
        }

        public State CurrentState { get; private set; }

        protected override string CurrentStateAsString => CurrentState.ToString();

        public FramingMode Mode
        {
            get
            {
                if (CurrentState != State.Done)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _mode;
            }
        }

        public int MajorVersion
        {
            get
            {
                if (CurrentState != State.Done)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _majorVersion;
            }
        }

        public int MinorVersion
        {
            get
            {
                if (CurrentState != State.Done)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

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

    // Used for Duplex/Simplex
    // Pattern:
    //   Start,
    //   (UpgradeRequest, upgrade-content-type)*,
    //   (EnvelopeStart, ReadingEnvelopeBytes*, EnvelopeEnd)*,
    //   End
    internal class ServerSessionDecoder : FramingDecoder
    {
        private readonly ViaStringDecoder _viaDecoder;
        private readonly StringDecoder _contentTypeDecoder;
        private IntDecoder _sizeDecoder;
        private string _contentType;
        private int _envelopeBytesNeeded;
        private int _envelopeSize;
        private string _upgrade;

        public ServerSessionDecoder(int maxViaLength, int maxContentTypeLength, ILogger logger) : base(logger)
        {
            _viaDecoder = new ViaStringDecoder(maxViaLength, logger);
            _contentTypeDecoder = new ContentTypeStringDecoder(maxContentTypeLength, logger);
            _sizeDecoder = new IntDecoder(logger);
            Reset();
        }

        public State CurrentState { get; private set; }

        protected override string CurrentStateAsString => CurrentState.ToString();

        public override string ContentType
        {
            get
            {
                if (CurrentState < State.PreUpgradeStart)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _contentType;
            }
        }

        public override Uri Via
        {
            get
            {
                if (CurrentState < State.ReadingContentTypeRecord)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _viaDecoder.ValueAsUri;
            }
        }

        public void Reset()
        {
            CurrentState = State.ReadingViaRecord;
        }

        public string Upgrade
        {
            get
            {
                if (CurrentState != State.UpgradeRequest)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _upgrade;
            }
        }

        public int EnvelopeSize
        {
            get
            {
                if (CurrentState < State.EnvelopeStart)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _envelopeSize;
            }
        }

        public override int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);
            ReadOnlySpan<byte> data = buffer.First.Span;
            try
            {
                int bytesConsumed;
                FramingRecordType recordType;
                Logger.LogStartState(this);
                switch (CurrentState)
                {
                    case State.ReadingViaRecord:
                        recordType = (FramingRecordType)data[0];
                        ValidateRecordType(FramingRecordType.Via, recordType);
                        bytesConsumed = 1;
                        _viaDecoder.Reset();
                        CurrentState = State.ReadingViaString;
                        break;
                    case State.ReadingViaString:
                        bytesConsumed = _viaDecoder.Decode(buffer);
                        if (_viaDecoder.IsValueDecoded)
                        {
                            CurrentState = State.ReadingContentTypeRecord;
                        }
                        break;
                    case State.ReadingContentTypeRecord:
                        recordType = (FramingRecordType)data[0];
                        if (recordType == FramingRecordType.KnownEncoding)
                        {
                            bytesConsumed = 1;
                            CurrentState = State.ReadingContentTypeByte;
                        }
                        else
                        {
                            ValidateRecordType(FramingRecordType.ExtensibleEncoding, recordType);
                            bytesConsumed = 1;
                            _contentTypeDecoder.Reset();
                            CurrentState = State.ReadingContentTypeString;
                        }
                        break;
                    case State.ReadingContentTypeByte:
                        _contentType = ContentTypeStringDecoder.GetString((FramingEncodingType)data[0]);
                        bytesConsumed = 1;
                        CurrentState = State.PreUpgradeStart;
                        break;
                    case State.ReadingContentTypeString:
                        bytesConsumed = _contentTypeDecoder.Decode(buffer);
                        if (_contentTypeDecoder.IsValueDecoded)
                        {
                            CurrentState = State.PreUpgradeStart;
                            _contentType = _contentTypeDecoder.Value;
                        }
                        break;
                    case State.PreUpgradeStart:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingUpgradeRecord;
                        break;
                    case State.ReadingUpgradeRecord:
                        recordType = (FramingRecordType)data[0];
                        if (recordType == FramingRecordType.UpgradeRequest)
                        {
                            bytesConsumed = 1;
                            _contentTypeDecoder.Reset();
                            CurrentState = State.ReadingUpgradeString;
                        }
                        else
                        {
                            bytesConsumed = 0;
                            CurrentState = State.ReadingPreambleEndRecord;
                        }
                        break;
                    case State.ReadingUpgradeString:
                        bytesConsumed = _contentTypeDecoder.Decode(buffer);
                        if (_contentTypeDecoder.IsValueDecoded)
                        {
                            CurrentState = State.UpgradeRequest;
                            _upgrade = _contentTypeDecoder.Value;
                        }
                        break;
                    case State.UpgradeRequest:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingUpgradeRecord;
                        break;
                    case State.ReadingPreambleEndRecord:
                        recordType = (FramingRecordType)data[0];
                        ValidateRecordType(FramingRecordType.PreambleEnd, recordType);
                        bytesConsumed = 1;
                        CurrentState = State.Start;
                        break;
                    case State.Start:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingEndRecord;
                        break;
                    case State.ReadingEndRecord:
                        recordType = (FramingRecordType)data[0];
                        if (recordType == FramingRecordType.End)
                        {
                            bytesConsumed = 1;
                            CurrentState = State.End;
                        }
                        else
                        {
                            bytesConsumed = 0;
                            CurrentState = State.ReadingEnvelopeRecord;
                        }
                        break;
                    case State.ReadingEnvelopeRecord:
                        ValidateRecordType(FramingRecordType.SizedEnvelope, (FramingRecordType)data[0]);
                        bytesConsumed = 1;
                        CurrentState = State.ReadingEnvelopeSize;
                        _sizeDecoder.Reset();
                        break;
                    case State.ReadingEnvelopeSize:
                        bytesConsumed = _sizeDecoder.Decode(buffer);
                        if (_sizeDecoder.IsValueDecoded)
                        {
                            CurrentState = State.EnvelopeStart;
                            _envelopeSize = _sizeDecoder.Value;
                            _envelopeBytesNeeded = _envelopeSize;
                        }
                        break;
                    case State.EnvelopeStart:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingEnvelopeBytes;
                        break;
                    case State.ReadingEnvelopeBytes:
                        bytesConsumed = (int)buffer.Length;
                        if (bytesConsumed > _envelopeBytesNeeded)
                        {
                            bytesConsumed = _envelopeBytesNeeded;
                        }

                        _envelopeBytesNeeded -= bytesConsumed;
                        if (_envelopeBytesNeeded == 0)
                        {
                            CurrentState = State.EnvelopeEnd;
                        }

                        break;
                    case State.EnvelopeEnd:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingEndRecord;
                        break;
                    case State.End:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                Logger.LogEndState(this, bytesConsumed);
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }

        public enum State
        {
            ReadingViaRecord,
            ReadingViaString,
            ReadingContentTypeRecord,
            ReadingContentTypeString,
            ReadingContentTypeByte,
            PreUpgradeStart,
            ReadingUpgradeRecord,
            ReadingUpgradeString,
            UpgradeRequest,
            ReadingPreambleEndRecord,
            Start,
            ReadingEnvelopeRecord,
            ReadingEnvelopeSize,
            EnvelopeStart,
            ReadingEnvelopeBytes,
            EnvelopeEnd,
            ReadingEndRecord,
            End,
        }
    }

    internal class SingletonMessageDecoder : FramingDecoder
    {
        private IntDecoder _sizeDecoder;
        private int _chunkBytesNeeded;
        private int _chunkSize;

        public SingletonMessageDecoder(ILogger logger) : base(logger)
        {
            _sizeDecoder = new IntDecoder(logger);
            CurrentState = State.ChunkStart;
        }

        public void Reset()
        {
            CurrentState = State.ChunkStart;
        }

        public State CurrentState { get; private set; }

        protected override string CurrentStateAsString => CurrentState.ToString();

        public int ChunkSize
        {
            get
            {
                if (CurrentState < State.ChunkStart)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _chunkSize;
            }
        }

        public override int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);
            ReadOnlySpan<byte> data = buffer.First.Span;
            try
            {
                int bytesConsumed;
                Logger.LogStartState(this);
                switch (CurrentState)
                {
                    case State.ReadingEnvelopeChunkSize:
                        bytesConsumed = _sizeDecoder.Decode(buffer);
                        if (_sizeDecoder.IsValueDecoded)
                        {
                            _chunkSize = _sizeDecoder.Value;
                            _sizeDecoder.Reset();

                            if (_chunkSize == 0)
                            {
                                CurrentState = State.EnvelopeEnd;
                            }
                            else
                            {
                                CurrentState = State.ChunkStart;
                                _chunkBytesNeeded = _chunkSize;
                            }
                        }
                        break;
                    case State.ChunkStart:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingEnvelopeBytes;
                        break;
                    case State.ReadingEnvelopeBytes:
                        bytesConsumed = (int)buffer.Length;
                        if (bytesConsumed > _chunkBytesNeeded)
                        {
                            bytesConsumed = _chunkBytesNeeded;
                        }
                        _chunkBytesNeeded -= bytesConsumed;
                        if (_chunkBytesNeeded == 0)
                        {
                            CurrentState = State.ChunkEnd;
                        }
                        break;
                    case State.ChunkEnd:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingEnvelopeChunkSize;
                        break;
                    case State.EnvelopeEnd:
                        ValidateRecordType(FramingRecordType.End, (FramingRecordType)data[0]);
                        bytesConsumed = 1;
                        CurrentState = State.End;
                        break;
                    case State.End:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));

                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                Logger.LogEndState(this, bytesConsumed);
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }

        public enum State
        {
            ReadingEnvelopeChunkSize,
            ChunkStart,
            ReadingEnvelopeBytes,
            ChunkEnd,
            EnvelopeEnd,
            End,
        }
    }

    // Pattern:
    //   Start,
    //   (UpgradeRequest, upgrade-bytes)*,
    //   EnvelopeStart,
    internal class ServerSingletonDecoder : FramingDecoder
    {
        private readonly ViaStringDecoder _viaDecoder;
        private readonly ContentTypeStringDecoder _contentTypeDecoder;
        private string _contentType;
        private string _upgrade;

        public ServerSingletonDecoder(int maxViaLength, int maxContentTypeLength, ILogger logger) : base(logger)
        {
            _viaDecoder = new ViaStringDecoder(maxViaLength, logger);
            _contentTypeDecoder = new ContentTypeStringDecoder(maxContentTypeLength, logger);
            Reset();
        }

        public void Reset()
        {
            CurrentState = State.ReadingViaRecord;
        }

        public State CurrentState { get; private set; }

        protected override string CurrentStateAsString => CurrentState.ToString();

        public override Uri Via
        {
            get
            {
                if (CurrentState < State.ReadingContentTypeRecord)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _viaDecoder.ValueAsUri;
            }
        }

        public override string ContentType
        {
            get
            {
                if (CurrentState < State.PreUpgradeStart)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _contentType;
            }
        }

        public string Upgrade
        {
            get
            {
                if (CurrentState != State.UpgradeRequest)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _upgrade;
            }
        }

        public override int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);
            ReadOnlySpan<byte> data = buffer.First.Span;
            try
            {
                int bytesConsumed;
                FramingRecordType recordType;
                Logger.LogStartState(this);
                switch (CurrentState)
                {
                    case State.ReadingViaRecord:
                        recordType = (FramingRecordType)data[0];
                        ValidateRecordType(FramingRecordType.Via, recordType);
                        bytesConsumed = 1;
                        _viaDecoder.Reset();
                        CurrentState = State.ReadingViaString;
                        break;
                    case State.ReadingViaString:
                        bytesConsumed = _viaDecoder.Decode(buffer);
                        if (_viaDecoder.IsValueDecoded)
                        {
                            CurrentState = State.ReadingContentTypeRecord;
                        }
                        break;
                    case State.ReadingContentTypeRecord:
                        recordType = (FramingRecordType)data[0];
                        if (recordType == FramingRecordType.KnownEncoding)
                        {
                            bytesConsumed = 1;
                            CurrentState = State.ReadingContentTypeByte;
                        }
                        else
                        {
                            ValidateRecordType(FramingRecordType.ExtensibleEncoding, recordType);
                            bytesConsumed = 1;
                            _contentTypeDecoder.Reset();
                            CurrentState = State.ReadingContentTypeString;
                        }
                        break;
                    case State.ReadingContentTypeByte:
                        _contentType = ContentTypeStringDecoder.GetString((FramingEncodingType)data[0]);
                        bytesConsumed = 1;
                        CurrentState = State.PreUpgradeStart;
                        break;
                    case State.ReadingContentTypeString:
                        bytesConsumed = _contentTypeDecoder.Decode(buffer);
                        if (_contentTypeDecoder.IsValueDecoded)
                        {
                            CurrentState = State.PreUpgradeStart;
                            _contentType = _contentTypeDecoder.Value;
                        }
                        break;
                    case State.PreUpgradeStart:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingUpgradeRecord;
                        break;
                    case State.ReadingUpgradeRecord:
                        recordType = (FramingRecordType)data[0];
                        if (recordType == FramingRecordType.UpgradeRequest)
                        {
                            bytesConsumed = 1;
                            _contentTypeDecoder.Reset();
                            CurrentState = State.ReadingUpgradeString;
                        }
                        else
                        {
                            bytesConsumed = 0;
                            CurrentState = State.ReadingPreambleEndRecord;
                        }
                        break;
                    case State.ReadingUpgradeString:
                        bytesConsumed = _contentTypeDecoder.Decode(buffer);
                        if (_contentTypeDecoder.IsValueDecoded)
                        {
                            CurrentState = State.UpgradeRequest;
                            _upgrade = _contentTypeDecoder.Value;
                        }
                        break;
                    case State.UpgradeRequest:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingUpgradeRecord;
                        break;
                    case State.ReadingPreambleEndRecord:
                        recordType = (FramingRecordType)data[0];
                        ValidateRecordType(FramingRecordType.PreambleEnd, recordType);
                        bytesConsumed = 1;
                        CurrentState = State.Start;
                        break;
                    case State.Start:
                        bytesConsumed = 0;
                        CurrentState = State.ReadingEnvelopeRecord;
                        break;
                    case State.ReadingEnvelopeRecord:
                        ValidateRecordType(FramingRecordType.UnsizedEnvelope, (FramingRecordType)data[0]);
                        bytesConsumed = 1;
                        CurrentState = State.EnvelopeStart;
                        break;
                    case State.EnvelopeStart:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                Logger.LogEndState(this, bytesConsumed);
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }

        public enum State
        {
            ReadingViaRecord,
            ReadingViaString,
            ReadingContentTypeRecord,
            ReadingContentTypeString,
            ReadingContentTypeByte,
            PreUpgradeStart,
            ReadingUpgradeRecord,
            ReadingUpgradeString,
            UpgradeRequest,
            ReadingPreambleEndRecord,
            Start,
            ReadingEnvelopeRecord,
            EnvelopeStart,
            ReadingEnvelopeChunkSize,
            ChunkStart,
            ReadingEnvelopeChunk,
            ChunkEnd,
            End,
        }
    }

    // Pattern:
    //   Start,
    //   EnvelopeStart,
    internal class ServerSingletonSizedDecoder : FramingDecoder
    {
        private readonly ViaStringDecoder _viaDecoder;
        private readonly ContentTypeStringDecoder _contentTypeDecoder;
        private string _contentType;

        public ServerSingletonSizedDecoder(int maxViaLength, int maxContentTypeLength, ILogger logger) : base(logger)
        {
            _viaDecoder = new ViaStringDecoder(maxViaLength, logger);
            _contentTypeDecoder = new ContentTypeStringDecoder(maxContentTypeLength, logger);
            CurrentState = State.ReadingViaRecord;
        }

        public override int Decode(ReadOnlySequence<byte> buffer)
        {
            DecoderHelper.ValidateSize(buffer.Length);
            ReadOnlySpan<byte> data = buffer.First.Span;
            try
            {
                int bytesConsumed;
                FramingRecordType recordType;
                Logger.LogStartState(this);
                switch (CurrentState)
                {
                    case State.ReadingViaRecord:
                        recordType = (FramingRecordType)data[0];
                        ValidateRecordType(FramingRecordType.Via, recordType);
                        bytesConsumed = 1;
                        _viaDecoder.Reset();
                        CurrentState = State.ReadingViaString;
                        break;
                    case State.ReadingViaString:
                        bytesConsumed = _viaDecoder.Decode(buffer);
                        if (_viaDecoder.IsValueDecoded)
                        {
                            CurrentState = State.ReadingContentTypeRecord;
                        }

                        break;
                    case State.ReadingContentTypeRecord:
                        recordType = (FramingRecordType)data[0];
                        if (recordType == FramingRecordType.KnownEncoding)
                        {
                            bytesConsumed = 1;
                            CurrentState = State.ReadingContentTypeByte;
                        }
                        else
                        {
                            ValidateRecordType(FramingRecordType.ExtensibleEncoding, recordType);
                            bytesConsumed = 1;
                            _contentTypeDecoder.Reset();
                            CurrentState = State.ReadingContentTypeString;
                        }
                        break;
                    case State.ReadingContentTypeByte:
                        _contentType = ContentTypeStringDecoder.GetString((FramingEncodingType)data[0]);
                        bytesConsumed = 1;
                        CurrentState = State.Start;
                        break;
                    case State.ReadingContentTypeString:
                        bytesConsumed = _contentTypeDecoder.Decode(buffer);
                        if (_contentTypeDecoder.IsValueDecoded)
                        {
                            CurrentState = State.Start;
                            _contentType = _contentTypeDecoder.Value;
                        }
                        break;
                    case State.Start:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                Logger.LogEndState(this, bytesConsumed);
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }

        public void Reset(long streamPosition)
        {
            CurrentState = State.ReadingViaRecord;
        }

        public State CurrentState { get; private set; }

        protected override string CurrentStateAsString => CurrentState.ToString();

        public override Uri Via
        {
            get
            {
                if (CurrentState < State.ReadingContentTypeRecord)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _viaDecoder.ValueAsUri;
            }
        }

        public override string ContentType
        {
            get
            {
                if (CurrentState < State.Start)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _contentType;
            }
        }

        public enum State
        {
            ReadingViaRecord,
            ReadingViaString,
            ReadingContentTypeRecord,
            ReadingContentTypeString,
            ReadingContentTypeByte,
            Start,
        }
    }
}
