// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    // TODO: Consider moving to primitives
    public abstract class ConnectionOrientedTransportBindingElement : TransportBindingElement
    {
        private int _connectionBufferSize;
        private readonly bool _exposeConnectionProperty;
        private HostNameComparisonMode _hostNameComparisonMode;
        private TimeSpan _channelInitializationTimeout;
        private int _maxBufferSize;
        private bool _maxBufferSizeInitialized;
        private int _maxPendingConnections;
        private TimeSpan _maxOutputDelay;
        private int _maxPendingAccepts;
        private TransferMode _transferMode;

        internal ConnectionOrientedTransportBindingElement()
            : base()
        {
            _connectionBufferSize = ConnectionOrientedTransportDefaults.ConnectionBufferSize;
            _hostNameComparisonMode = ConnectionOrientedTransportDefaults.HostNameComparisonMode;
            _channelInitializationTimeout = ConnectionOrientedTransportDefaults.ChannelInitializationTimeout;
            _maxBufferSize = TransportDefaults.MaxBufferSize;
            _maxPendingConnections = ConnectionOrientedTransportDefaults.GetMaxPendingConnections();
            _maxOutputDelay = ConnectionOrientedTransportDefaults.MaxOutputDelay;
            _maxPendingAccepts = ConnectionOrientedTransportDefaults.GetMaxPendingAccepts();
            _transferMode = ConnectionOrientedTransportDefaults.TransferMode;
        }

        internal ConnectionOrientedTransportBindingElement(ConnectionOrientedTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _connectionBufferSize = elementToBeCloned._connectionBufferSize;
            _exposeConnectionProperty = elementToBeCloned._exposeConnectionProperty;
            _hostNameComparisonMode = elementToBeCloned._hostNameComparisonMode;
            InheritBaseAddressSettings = elementToBeCloned.InheritBaseAddressSettings;
            _channelInitializationTimeout = elementToBeCloned.ChannelInitializationTimeout;
            _maxBufferSize = elementToBeCloned._maxBufferSize;
            _maxBufferSizeInitialized = elementToBeCloned._maxBufferSizeInitialized;
            _maxPendingConnections = elementToBeCloned._maxPendingConnections;
            _maxOutputDelay = elementToBeCloned._maxOutputDelay;
            _maxPendingAccepts = elementToBeCloned._maxPendingAccepts;
            _transferMode = elementToBeCloned._transferMode;
            IsMaxPendingConnectionsSet = elementToBeCloned.IsMaxPendingConnectionsSet;
            IsMaxPendingAcceptsSet = elementToBeCloned.IsMaxPendingAcceptsSet;
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.ConnectionBufferSize)]
        public int ConnectionBufferSize
        {
            get
            {
                return _connectionBufferSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBeNonNegative));
                }

                _connectionBufferSize = value;
            }
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.HostNameComparisonMode)]
        public HostNameComparisonMode HostNameComparisonMode
        {
            get
            {
                return _hostNameComparisonMode;
            }

            set
            {
                HostNameComparisonModeHelper.Validate(value);
                _hostNameComparisonMode = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get
            {
                if (_maxBufferSizeInitialized || TransferMode != TransferMode.Buffered)
                {
                    return _maxBufferSize;
                }

                long maxReceivedMessageSize = MaxReceivedMessageSize;
                if (maxReceivedMessageSize > int.MaxValue)
                {
                    return int.MaxValue;
                }
                else
                {
                    return (int)maxReceivedMessageSize;
                }
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }

                _maxBufferSizeInitialized = true;
                _maxBufferSize = value;
            }
        }

        public int MaxPendingConnections
        {
            get
            {
                return _maxPendingConnections;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }

                _maxPendingConnections = value;
                IsMaxPendingConnectionsSet = true;
            }
        }

        internal bool IsMaxPendingConnectionsSet { get; private set; }

        // used by MEX to ensure that we don't conflict on base-address scoped settings
        internal bool InheritBaseAddressSettings { get; set; }

        public TimeSpan ChannelInitializationTimeout
        {
            get
            {
                return _channelInitializationTimeout;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                _channelInitializationTimeout = value;
            }
        }

        public TimeSpan MaxOutputDelay
        {
            get
            {
                return _maxOutputDelay;
            }

            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRange0));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                _maxOutputDelay = value;
            }
        }

        public int MaxPendingAccepts
        {
            get
            {
                return _maxPendingAccepts;
            }

            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }

                _maxPendingAccepts = value;
                IsMaxPendingAcceptsSet = true;
            }
        }

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            return true;
        }

        internal bool IsMaxPendingAcceptsSet { get; private set; }

        [DefaultValue(ConnectionOrientedTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get
            {
                return _transferMode;
            }
            set
            {
                TransferModeHelper.Validate(value);
                _transferMode = value;
            }
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(TransferMode))
            {
                return (T)(object)TransferMode;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (!base.IsMatch(b))
            {
                return false;
            }

            if (!(b is ConnectionOrientedTransportBindingElement connection))
            {
                return false;
            }

            if (_connectionBufferSize != connection._connectionBufferSize)
            {
                return false;
            }

            if (_hostNameComparisonMode != connection._hostNameComparisonMode)
            {
                return false;
            }

            if (InheritBaseAddressSettings != connection.InheritBaseAddressSettings)
            {
                return false;
            }

            if (_channelInitializationTimeout != connection._channelInitializationTimeout)
            {
                return false;
            }
            if (_maxBufferSize != connection._maxBufferSize)
            {
                return false;
            }

            if (_maxPendingConnections != connection._maxPendingConnections)
            {
                return false;
            }

            if (_maxOutputDelay != connection._maxOutputDelay)
            {
                return false;
            }

            if (_maxPendingAccepts != connection._maxPendingAccepts)
            {
                return false;
            }

            if (_transferMode != connection._transferMode)
            {
                return false;
            }

            return true;
        }
    }
}
