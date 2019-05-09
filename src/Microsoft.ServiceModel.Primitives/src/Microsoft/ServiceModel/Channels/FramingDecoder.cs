using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Runtime;

namespace Microsoft.ServiceModel.Channels
{
    internal static class DecoderHelper
    {
        public static void ValidateSize(int size)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("size", size, SR.ValueMustBePositive));
            }
        }
    }

    internal struct IntDecoder
    {
        private int _value;
        private short _index;
        private bool _isValueDecoded;
        private const int LastIndex = 4;

        public int Value
        {
            get
            {
                if (!_isValueDecoded)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                return _value;
            }
        }

        public bool IsValueDecoded
        {
            get { return _isValueDecoded; }
        }

        public void Reset()
        {
            _index = 0;
            _value = 0;
            _isValueDecoded = false;
        }

        public int Decode(byte[] buffer, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);
            if (_isValueDecoded)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
            }
            int bytesConsumed = 0;
            while (bytesConsumed < size)
            {
                int next = buffer[offset];
                _value |= (next & 0x7F) << (_index * 7);
                bytesConsumed++;
                if (_index == LastIndex && (next & 0xF8) != 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.FramingSizeTooLarge));
                }
                _index++;
                if ((next & 0x80) == 0)
                {
                    _isValueDecoded = true;
                    break;
                }
                offset++;
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
        private int _sizeQuota;
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
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
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(quotaExceeded);
                        }
                        if (_encodedBytes == null || _encodedBytes.Length < _encodedSize)
                        {
                            _encodedBytes = Fx.AllocateByteArray(_encodedSize);
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.InvalidDecoderStateMachine));
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

    internal class ViaStringDecoder : StringDecoder
    {
        private Uri _via;

        public ViaStringDecoder(int sizeQuota)
            : base(sizeQuota)
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                return _via;
            }
        }
    }

    internal class FaultStringDecoder : StringDecoder
    {
        internal const int FaultSizeQuota = 256;

        public FaultStringDecoder()
            : base(FaultSizeQuota)
        {
        }

        protected override Exception OnSizeQuotaExceeded(int size)
        {
            return new InvalidDataException(SR.Format(SR.FramingFaultTooLong, size));
        }

        public static Exception GetFaultException(string faultString, string via, string contentType)
        {
            if (faultString == FramingEncodingString.EndpointNotFoundFault)
            {
                return new EndpointNotFoundException(SR.Format(SR.EndpointNotFound, via));
            }
            else if (faultString == FramingEncodingString.ContentTypeInvalidFault)
            {
                return new ProtocolException(SR.Format(SR.FramingContentTypeMismatch, contentType, via));
            }
            else if (faultString == FramingEncodingString.ServiceActivationFailedFault)
            {
                // TODO: Bring in exception or leave as is
                //return new ServiceActivationException(SR.Format(SR.Hosting_ServiceActivationFailed, via));
                return new CommunicationException(SR.Format(SR.Hosting_ServiceActivationFailed, via));
            }
            else if (faultString == FramingEncodingString.ConnectionDispatchFailedFault)
            {
                return new CommunicationException(SR.Format(SR.Sharing_ConnectionDispatchFailed, via));
            }
            else if (faultString == FramingEncodingString.EndpointUnavailableFault)
            {
                return new EndpointNotFoundException(SR.Format(SR.Sharing_EndpointUnavailable, via));
            }
            else if (faultString == FramingEncodingString.MaxMessageSizeExceededFault)
            {
                Exception inner = new QuotaExceededException(SR.FramingMaxMessageSizeExceeded);
                return new CommunicationException(inner.Message, inner);
            }
            else if (faultString == FramingEncodingString.UnsupportedModeFault)
            {
                return new ProtocolException(SR.Format(SR.FramingModeNotSupportedFault, via));
            }
            else if (faultString == FramingEncodingString.UnsupportedVersionFault)
            {
                return new ProtocolException(SR.Format(SR.FramingVersionNotSupportedFault, via));
            }
            else if (faultString == FramingEncodingString.ContentTypeTooLongFault)
            {
                Exception inner = new QuotaExceededException(SR.Format(SR.FramingContentTypeTooLongFault, contentType));
                return new CommunicationException(inner.Message, inner);
            }
            else if (faultString == FramingEncodingString.ViaTooLongFault)
            {
                Exception inner = new QuotaExceededException(SR.Format(SR.FramingViaTooLongFault, via));
                return new CommunicationException(inner.Message, inner);
            }
            else if (faultString == FramingEncodingString.ServerTooBusyFault)
            {
                return new ServerTooBusyException(SR.Format(SR.ServerTooBusy, via));
            }
            else if (faultString == FramingEncodingString.UpgradeInvalidFault)
            {
                return new ProtocolException(SR.Format(SR.FramingUpgradeInvalid, via));
            }
            else
            {
                return new ProtocolException(SR.Format(SR.FramingFaultUnrecognized, faultString));
            }
        }
    }

    internal class ContentTypeStringDecoder : StringDecoder
    {
        public ContentTypeStringDecoder(int sizeQuota)
            : base(sizeQuota)
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
                default:
                    return "unknown" + ((int)type).ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    internal abstract class FramingDecoder
    {
        private long _streamPosition;

        protected FramingDecoder()
        {
        }

        protected FramingDecoder(long streamPosition)
        {
            _streamPosition = streamPosition;
        }

        protected abstract string CurrentStateAsString { get; }

        public long StreamPosition
        {
            get { return _streamPosition; }
            set { _streamPosition = value; }
        }

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
            return new ProtocolException(SR.Format(SR.FramingError, StreamPosition, CurrentStateAsString),
                innerException);
        }
    }

    internal class SingletonMessageDecoder : FramingDecoder
    {
        private IntDecoder _sizeDecoder;
        private int _chunkBytesNeeded;
        private int _chunkSize;
        private State _currentState;

        public SingletonMessageDecoder(long streamPosition)
            : base(streamPosition)
        {
            _sizeDecoder = new IntDecoder();
            _currentState = State.ChunkStart;
        }

        public void Reset()
        {
            _currentState = State.ChunkStart;
        }

        public State CurrentState
        {
            get { return _currentState; }
        }

        protected override string CurrentStateAsString
        {
            get { return _currentState.ToString(); }
        }

        public int ChunkSize
        {
            get
            {
                if (_currentState < State.ChunkStart)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                }

                return _chunkSize;
            }
        }

        public int Decode(byte[] bytes, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);

            try
            {
                int bytesConsumed;
                switch (_currentState)
                {
                    case State.ReadingEnvelopeChunkSize:
                        bytesConsumed = _sizeDecoder.Decode(bytes, offset, size);
                        if (_sizeDecoder.IsValueDecoded)
                        {
                            _chunkSize = _sizeDecoder.Value;
                            _sizeDecoder.Reset();

                            if (_chunkSize == 0)
                            {
                                _currentState = State.EnvelopeEnd;
                            }
                            else
                            {
                                _currentState = State.ChunkStart;
                                _chunkBytesNeeded = _chunkSize;
                            }
                        }
                        break;
                    case State.ChunkStart:
                        bytesConsumed = 0;
                        _currentState = State.ReadingEnvelopeBytes;
                        break;
                    case State.ReadingEnvelopeBytes:
                        bytesConsumed = size;
                        if (bytesConsumed > _chunkBytesNeeded)
                        {
                            bytesConsumed = _chunkBytesNeeded;
                        }
                        _chunkBytesNeeded -= bytesConsumed;
                        if (_chunkBytesNeeded == 0)
                        {
                            _currentState = State.ChunkEnd;
                        }
                        break;
                    case State.ChunkEnd:
                        bytesConsumed = 0;
                        _currentState = State.ReadingEnvelopeChunkSize;
                        break;
                    case State.EnvelopeEnd:
                        ValidateRecordType(FramingRecordType.End, (FramingRecordType)bytes[offset]);
                        bytesConsumed = 1;
                        _currentState = State.End;
                        break;
                    case State.End:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));

                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                StreamPosition += bytesConsumed;
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

    // common set of states used on the client-side.
    internal enum ClientFramingDecoderState
    {
        ReadingUpgradeRecord,
        ReadingUpgradeMode,
        UpgradeResponse,
        ReadingAckRecord,
        Start,
        ReadingFault,
        ReadingFaultString,
        Fault,
        ReadingEnvelopeRecord,
        ReadingEnvelopeSize,
        EnvelopeStart,
        ReadingEnvelopeBytes,
        EnvelopeEnd,
        ReadingEndRecord,
        End,
    }

    internal abstract class ClientFramingDecoder : FramingDecoder
    {
        private ClientFramingDecoderState _currentState;

        protected ClientFramingDecoder(long streamPosition)
            : base(streamPosition)
        {
            _currentState = ClientFramingDecoderState.ReadingUpgradeRecord;
        }

        public ClientFramingDecoderState CurrentState
        {
            get
            {
                return _currentState;
            }

            protected set
            {
                _currentState = value;
            }
        }

        protected override string CurrentStateAsString
        {
            get { return _currentState.ToString(); }
        }

        public abstract string Fault
        {
            get;
        }

        public abstract int Decode(byte[] bytes, int offset, int size);
    }

    // Pattern: 
    //   (UpgradeResponse, upgrade-bytes)*, (Ack | Fault),
    //   ((EnvelopeStart, ReadingEnvelopeBytes*, EnvelopeEnd) | Fault)*, 
    //   End
    internal class ClientDuplexDecoder : ClientFramingDecoder
    {
        private IntDecoder _sizeDecoder;
        private FaultStringDecoder _faultDecoder;
        private int _envelopeBytesNeeded;
        private int _envelopeSize;

        public ClientDuplexDecoder(long streamPosition)
            : base(streamPosition)
        {
            _sizeDecoder = new IntDecoder();
        }

        public int EnvelopeSize
        {
            get
            {
                if (CurrentState < ClientFramingDecoderState.EnvelopeStart)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                return _envelopeSize;
            }
        }

        public override string Fault
        {
            get
            {
                if (CurrentState < ClientFramingDecoderState.Fault)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                return _faultDecoder.Value;
            }
        }

        public override int Decode(byte[] bytes, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);

            try
            {
                int bytesConsumed;
                FramingRecordType recordType;
                switch (CurrentState)
                {
                    case ClientFramingDecoderState.ReadingUpgradeRecord:
                        recordType = (FramingRecordType)bytes[offset];
                        if (recordType == FramingRecordType.UpgradeResponse)
                        {
                            bytesConsumed = 1;
                            base.CurrentState = ClientFramingDecoderState.UpgradeResponse;
                        }
                        else
                        {
                            bytesConsumed = 0;
                            base.CurrentState = ClientFramingDecoderState.ReadingAckRecord;
                        }
                        break;
                    case ClientFramingDecoderState.UpgradeResponse:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingUpgradeRecord;
                        break;
                    case ClientFramingDecoderState.ReadingAckRecord:
                        recordType = (FramingRecordType)bytes[offset];
                        if (recordType == FramingRecordType.Fault)
                        {
                            bytesConsumed = 1;
                            _faultDecoder = new FaultStringDecoder();
                            base.CurrentState = ClientFramingDecoderState.ReadingFaultString;
                            break;
                        }
                        ValidatePreambleAck(recordType);
                        bytesConsumed = 1;
                        base.CurrentState = ClientFramingDecoderState.Start;
                        break;
                    case ClientFramingDecoderState.Start:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingEnvelopeRecord;
                        break;
                    case ClientFramingDecoderState.ReadingEnvelopeRecord:
                        recordType = (FramingRecordType)bytes[offset];
                        if (recordType == FramingRecordType.End)
                        {
                            bytesConsumed = 1;
                            base.CurrentState = ClientFramingDecoderState.End;
                            break;
                        }
                        else if (recordType == FramingRecordType.Fault)
                        {
                            bytesConsumed = 1;
                            _faultDecoder = new FaultStringDecoder();
                            base.CurrentState = ClientFramingDecoderState.ReadingFaultString;
                            break;
                        }
                        ValidateRecordType(FramingRecordType.SizedEnvelope, recordType);
                        bytesConsumed = 1;
                        base.CurrentState = ClientFramingDecoderState.ReadingEnvelopeSize;
                        _sizeDecoder.Reset();
                        break;
                    case ClientFramingDecoderState.ReadingEnvelopeSize:
                        bytesConsumed = _sizeDecoder.Decode(bytes, offset, size);
                        if (_sizeDecoder.IsValueDecoded)
                        {
                            base.CurrentState = ClientFramingDecoderState.EnvelopeStart;
                            _envelopeSize = _sizeDecoder.Value;
                            _envelopeBytesNeeded = _envelopeSize;
                        }
                        break;
                    case ClientFramingDecoderState.EnvelopeStart:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingEnvelopeBytes;
                        break;
                    case ClientFramingDecoderState.ReadingEnvelopeBytes:
                        bytesConsumed = size;
                        if (bytesConsumed > _envelopeBytesNeeded)
                            bytesConsumed = _envelopeBytesNeeded;
                        _envelopeBytesNeeded -= bytesConsumed;
                        if (_envelopeBytesNeeded == 0)
                            base.CurrentState = ClientFramingDecoderState.EnvelopeEnd;
                        break;
                    case ClientFramingDecoderState.EnvelopeEnd:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingEnvelopeRecord;
                        break;
                    case ClientFramingDecoderState.ReadingFaultString:
                        bytesConsumed = _faultDecoder.Decode(bytes, offset, size);
                        if (_faultDecoder.IsValueDecoded)
                        {
                            base.CurrentState = ClientFramingDecoderState.Fault;
                        }
                        break;
                    case ClientFramingDecoderState.Fault:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingEndRecord;
                        break;
                    case ClientFramingDecoderState.ReadingEndRecord:
                        ValidateRecordType(FramingRecordType.End, (FramingRecordType)bytes[offset]);
                        bytesConsumed = 1;
                        base.CurrentState = ClientFramingDecoderState.End;
                        break;
                    case ClientFramingDecoderState.End:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                StreamPosition += bytesConsumed;
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }
    }

    // Pattern: 
    //   (UpgradeResponse, upgrade-bytes)*, (Ack | Fault),
    //   End
    internal class ClientSingletonDecoder : ClientFramingDecoder
    {
        private FaultStringDecoder _faultDecoder;

        public ClientSingletonDecoder(long streamPosition)
            : base(streamPosition)
        {
        }

        public override string Fault
        {
            get
            {
                if (CurrentState < ClientFramingDecoderState.Fault)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.FramingValueNotAvailable));
                return _faultDecoder.Value;
            }
        }

        public override int Decode(byte[] bytes, int offset, int size)
        {
            DecoderHelper.ValidateSize(size);

            try
            {
                int bytesConsumed;
                FramingRecordType recordType;
                switch (CurrentState)
                {
                    case ClientFramingDecoderState.ReadingUpgradeRecord:
                        recordType = (FramingRecordType)bytes[offset];
                        if (recordType == FramingRecordType.UpgradeResponse)
                        {
                            bytesConsumed = 1;
                            base.CurrentState = ClientFramingDecoderState.UpgradeResponse;
                        }
                        else
                        {
                            bytesConsumed = 0;
                            base.CurrentState = ClientFramingDecoderState.ReadingAckRecord;
                        }
                        break;
                    case ClientFramingDecoderState.UpgradeResponse:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingUpgradeRecord;
                        break;
                    case ClientFramingDecoderState.ReadingAckRecord:
                        recordType = (FramingRecordType)bytes[offset];
                        if (recordType == FramingRecordType.Fault)
                        {
                            bytesConsumed = 1;
                            _faultDecoder = new FaultStringDecoder();
                            base.CurrentState = ClientFramingDecoderState.ReadingFaultString;
                            break;
                        }
                        ValidatePreambleAck(recordType);
                        bytesConsumed = 1;
                        base.CurrentState = ClientFramingDecoderState.Start;
                        break;

                    case ClientFramingDecoderState.Start:
                        bytesConsumed = 0;
                        base.CurrentState = ClientFramingDecoderState.ReadingEnvelopeRecord;
                        break;

                    case ClientFramingDecoderState.ReadingEnvelopeRecord:
                        recordType = (FramingRecordType)bytes[offset];
                        if (recordType == FramingRecordType.End)
                        {
                            bytesConsumed = 1;
                            base.CurrentState = ClientFramingDecoderState.End;
                            break;
                        }
                        else if (recordType == FramingRecordType.Fault)
                        {
                            bytesConsumed = 0;
                            base.CurrentState = ClientFramingDecoderState.ReadingFault;
                            break;
                        }
                        ValidateRecordType(FramingRecordType.UnsizedEnvelope, recordType);
                        bytesConsumed = 1;
                        base.CurrentState = ClientFramingDecoderState.EnvelopeStart;
                        break;

                    case ClientFramingDecoderState.EnvelopeStart:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));

                    case ClientFramingDecoderState.ReadingFault:
                        recordType = (FramingRecordType)bytes[offset];
                        ValidateRecordType(FramingRecordType.Fault, recordType);
                        bytesConsumed = 1;
                        _faultDecoder = new FaultStringDecoder();
                        base.CurrentState = ClientFramingDecoderState.ReadingFaultString;
                        break;
                    case ClientFramingDecoderState.ReadingFaultString:
                        bytesConsumed = _faultDecoder.Decode(bytes, offset, size);
                        if (_faultDecoder.IsValueDecoded)
                        {
                            base.CurrentState = ClientFramingDecoderState.Fault;
                        }
                        break;
                    case ClientFramingDecoderState.Fault:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.FramingAtEnd)));
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            CreateException(new InvalidDataException(SR.InvalidDecoderStateMachine)));
                }

                StreamPosition += bytesConsumed;
                return bytesConsumed;
            }
            catch (InvalidDataException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateException(e));
            }
        }
    }

}