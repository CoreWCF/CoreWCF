// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.WebSockets;
using System.Threading;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class WebSocketTransportSettings : IEquatable<WebSocketTransportSettings>
    {
        public const string ConnectionOpenedAction = "http://schemas.microsoft.com/2011/02/session/onopen";
        public const string BinaryMessageReceivedAction = "http://schemas.microsoft.com/2011/02/websockets/onbinarymessage";
        public const string TextMessageReceivedAction = "http://schemas.microsoft.com/2011/02/websockets/ontextmessage";
        public const string SoapContentTypeHeader = "soap-content-type";
        public const string BinaryEncoderTransferModeHeader = "microsoft-binary-transfer-mode";
        internal const string WebSocketMethod = "WEBSOCKET";
        internal const string SoapSubProtocol = "soap";
        internal const string TransportUsageMethodName = "TransportUsage";
        private WebSocketTransportUsage _transportUsage;
        private TimeSpan _keepAliveInterval;
        private string _subProtocol;
        private int _maxPendingConnections;

        public WebSocketTransportSettings()
        {
            _transportUsage = WebSocketDefaults.TransportUsage;
            CreateNotificationOnConnection = WebSocketDefaults.CreateNotificationOnConnection;
            _keepAliveInterval = WebSocketDefaults.DefaultKeepAliveInterval;
            _subProtocol = WebSocketDefaults.SubProtocol;
            DisablePayloadMasking = WebSocketDefaults.DisablePayloadMasking;
            _maxPendingConnections = WebSocketDefaults.DefaultMaxPendingConnections;
        }

        private WebSocketTransportSettings(WebSocketTransportSettings settings)
        {
            Fx.Assert(settings != null, "settings should not be null.");
            TransportUsage = settings.TransportUsage;
            SubProtocol = settings.SubProtocol;
            KeepAliveInterval = settings.KeepAliveInterval;
            DisablePayloadMasking = settings.DisablePayloadMasking;
            CreateNotificationOnConnection = settings.CreateNotificationOnConnection;
            MaxPendingConnections = settings.MaxPendingConnections;
        }

        public WebSocketTransportUsage TransportUsage
        {
            get
            {
                return _transportUsage;
            }

            set
            {
                WebSocketTransportUsageHelper.Validate(value);
                _transportUsage = value;
            }
        }

        public bool CreateNotificationOnConnection { get; set; }

        public TimeSpan KeepAliveInterval
        {
            get
            {
                return _keepAliveInterval;
            }

            set
            {
                if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(
                                nameof(value),
                                value,
                                SRCommon.SFxTimeoutOutOfRange0));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(
                                            nameof(value),
                                            value,
                                            SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _keepAliveInterval = value;
            }
        }

        public string SubProtocol
        {
            get
            {
                return _subProtocol;
            }

            set
            {
                if (value != null)
                {
                    if (value == string.Empty)
                    {
                        throw Fx.Exception.Argument(nameof(value), SR.WebSocketInvalidProtocolEmptySubprotocolString);
                    }

                    if (value.Split(WebSocketHelper.ProtocolSeparators).Length > 1)
                    {
                        throw Fx.Exception.Argument(nameof(value), SR.Format(SR.WebSocketInvalidProtocolContainsMultipleSubProtocolString, value));
                    }

                    if (WebSocketHelper.IsSubProtocolInvalid(value, out string invalidChar))
                    {
                        throw Fx.Exception.Argument(nameof(value), SR.Format(SR.WebSocketInvalidProtocolInvalidCharInProtocolString, value, invalidChar));
                    }
                }

                _subProtocol = value;
            }
        }

        public bool DisablePayloadMasking { get; set; }

        public int MaxPendingConnections
        {
            get
            {
                return _maxPendingConnections;
            }

            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        SRCommon.ValueMustBePositive));
                }

                _maxPendingConnections = value;
            }
        }

        public bool Equals(WebSocketTransportSettings other)
        {
            if (other == null)
            {
                return false;
            }

            return TransportUsage == other.TransportUsage
                && CreateNotificationOnConnection == other.CreateNotificationOnConnection
                && KeepAliveInterval == other.KeepAliveInterval
                && DisablePayloadMasking == other.DisablePayloadMasking
                && StringComparer.OrdinalIgnoreCase.Compare(SubProtocol, other.SubProtocol) == 0
                && MaxPendingConnections == other.MaxPendingConnections;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return base.Equals(obj);
            }

            WebSocketTransportSettings settings = obj as WebSocketTransportSettings;
            return Equals(settings);
        }

        public override int GetHashCode()
        {
            int hashcode = TransportUsage.GetHashCode()
                        ^ CreateNotificationOnConnection.GetHashCode()
                        ^ KeepAliveInterval.GetHashCode()
                        ^ DisablePayloadMasking.GetHashCode()
                        ^ MaxPendingConnections.GetHashCode();
            if (SubProtocol != null)
            {
                hashcode ^= SubProtocol.ToLowerInvariant().GetHashCode();
            }

            return hashcode;
        }

        internal WebSocketTransportSettings Clone()
        {
            return new WebSocketTransportSettings(this);
        }

        internal TimeSpan GetEffectiveKeepAliveInterval()
        {
            return _keepAliveInterval == TimeSpan.Zero ? WebSocket.DefaultKeepAliveInterval : _keepAliveInterval;
        }
    }
}
