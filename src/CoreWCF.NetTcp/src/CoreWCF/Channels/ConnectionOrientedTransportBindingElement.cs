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
        private int connectionBufferSize;
        private readonly bool exposeConnectionProperty;
        private HostNameComparisonMode hostNameComparisonMode;
        private bool inheritBaseAddressSettings;
        private TimeSpan channelInitializationTimeout;
        private int maxBufferSize;
        private bool maxBufferSizeInitialized;
        private int maxPendingConnections;
        private TimeSpan maxOutputDelay;
        private int maxPendingAccepts;
        private TransferMode transferMode;
        private bool isMaxPendingConnectionsSet;
        private bool isMaxPendingAcceptsSet;

        internal ConnectionOrientedTransportBindingElement()
            : base()
        {
            connectionBufferSize = ConnectionOrientedTransportDefaults.ConnectionBufferSize;
            hostNameComparisonMode = ConnectionOrientedTransportDefaults.HostNameComparisonMode;
            channelInitializationTimeout = ConnectionOrientedTransportDefaults.ChannelInitializationTimeout;
            maxBufferSize = TransportDefaults.MaxBufferSize;
            maxPendingConnections = ConnectionOrientedTransportDefaults.GetMaxPendingConnections();
            maxOutputDelay = ConnectionOrientedTransportDefaults.MaxOutputDelay;
            maxPendingAccepts = ConnectionOrientedTransportDefaults.GetMaxPendingAccepts();
            transferMode = ConnectionOrientedTransportDefaults.TransferMode;
        }

        internal ConnectionOrientedTransportBindingElement(ConnectionOrientedTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            connectionBufferSize = elementToBeCloned.connectionBufferSize;
            exposeConnectionProperty = elementToBeCloned.exposeConnectionProperty;
            hostNameComparisonMode = elementToBeCloned.hostNameComparisonMode;
            inheritBaseAddressSettings = elementToBeCloned.InheritBaseAddressSettings;
            channelInitializationTimeout = elementToBeCloned.ChannelInitializationTimeout;
            maxBufferSize = elementToBeCloned.maxBufferSize;
            maxBufferSizeInitialized = elementToBeCloned.maxBufferSizeInitialized;
            maxPendingConnections = elementToBeCloned.maxPendingConnections;
            maxOutputDelay = elementToBeCloned.maxOutputDelay;
            maxPendingAccepts = elementToBeCloned.maxPendingAccepts;
            transferMode = elementToBeCloned.transferMode;
            isMaxPendingConnectionsSet = elementToBeCloned.isMaxPendingConnectionsSet;
            isMaxPendingAcceptsSet = elementToBeCloned.isMaxPendingAcceptsSet;
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.ConnectionBufferSize)]
        public int ConnectionBufferSize
        {
            get
            {
                return connectionBufferSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.ValueMustBeNonNegative));
                }

                connectionBufferSize = value;
            }
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.HostNameComparisonMode)]
        public HostNameComparisonMode HostNameComparisonMode
        {
            get
            {
                return hostNameComparisonMode;
            }

            set
            {
                HostNameComparisonModeHelper.Validate(value);
                hostNameComparisonMode = value;
            }
        }

        [DefaultValue(TransportDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get
            {
                if (maxBufferSizeInitialized || TransferMode != TransferMode.Buffered)
                {
                    return maxBufferSize;
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.ValueMustBePositive));
                }

                maxBufferSizeInitialized = true;
                maxBufferSize = value;
            }
        }

        public int MaxPendingConnections
        {
            get
            {
                return maxPendingConnections;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.ValueMustBePositive));
                }

                maxPendingConnections = value;
                isMaxPendingConnectionsSet = true;
            }
        }

        internal bool IsMaxPendingConnectionsSet
        {
            get { return isMaxPendingConnectionsSet; }
        }

        // used by MEX to ensure that we don't conflict on base-address scoped settings
        internal bool InheritBaseAddressSettings
        {
            get
            {
                return inheritBaseAddressSettings;
            }
            set
            {
                inheritBaseAddressSettings = value;
            }
        }

        public TimeSpan ChannelInitializationTimeout
        {
            get
            {
                return channelInitializationTimeout;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                channelInitializationTimeout = value;
            }
        }

        public TimeSpan MaxOutputDelay
        {
            get
            {
                return maxOutputDelay;
            }

            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.SFxTimeoutOutOfRange0));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.SFxTimeoutOutOfRangeTooBig));
                }

                maxOutputDelay = value;
            }
        }

        public int MaxPendingAccepts
        {
            get
            {
                return maxPendingAccepts;
            }

            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.ValueMustBePositive));
                }

                maxPendingAccepts = value;
                isMaxPendingAcceptsSet = true;
            }
        }

        internal bool IsMaxPendingAcceptsSet
        {
            get { return isMaxPendingAcceptsSet; }
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.TransferMode)]
        public TransferMode TransferMode
        {
            get
            {
                return transferMode;
            }
            set
            {
                TransferModeHelper.Validate(value);
                transferMode = value;
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

            ConnectionOrientedTransportBindingElement connection = b as ConnectionOrientedTransportBindingElement;
            if (connection == null)
            {
                return false;
            }

            if (connectionBufferSize != connection.connectionBufferSize)
            {
                return false;
            }

            if (hostNameComparisonMode != connection.hostNameComparisonMode)
            {
                return false;
            }

            if (inheritBaseAddressSettings != connection.inheritBaseAddressSettings)
            {
                return false;
            }

            if (channelInitializationTimeout != connection.channelInitializationTimeout)
            {
                return false;
            }
            if (maxBufferSize != connection.maxBufferSize)
            {
                return false;
            }

            if (maxPendingConnections != connection.maxPendingConnections)
            {
                return false;
            }

            if (maxOutputDelay != connection.maxOutputDelay)
            {
                return false;
            }

            if (maxPendingAccepts != connection.maxPendingAccepts)
            {
                return false;
            }

            if (transferMode != connection.transferMode)
            {
                return false;
            }

            return true;
        }
    }
}