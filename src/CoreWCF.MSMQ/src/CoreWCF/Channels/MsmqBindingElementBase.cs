// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class MsmqBindingElementBase : QueueBaseTransportBindingElement
    {
        private DeadLetterQueue _deadLetterQueue;
        private int _maxRetryCycles;
        private int _receiveRetryCount;
        private TimeSpan _retryCycleDelay;
        private TimeSpan _timeToLive;

        //private ReceiveErrorHandling receiveErrorHandling;

        public Uri CustomDeadLetterQueue { get; set; }

        public DeadLetterQueue DeadLetterQueue
        {
            get { return _deadLetterQueue; }
            set
            {
                if (!DeadLetterQueueHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _deadLetterQueue = value;
            }
        }

        public bool ExactlyOnce { get; set; }

        // todo: need to be enabled when we add System.Transactions support
        //public int ReceiveRetryCount
        //{
        //    get { return _receiveRetryCount; }
        //    set
        //    {
        //        if (value < 0)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //                new ArgumentOutOfRangeException(nameof(value), value, "MsmqNonNegativeArgumentExpected"));
        //        }

        //        _receiveRetryCount = value;
        //    }
        //}

        // todo: need to be enabled when we add System.Transactions support
        //public int MaxRetryCycles
        //{
        //    get { return _maxRetryCycles; }
        //    set
        //    {
        //        if (value < 0)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //                new ArgumentOutOfRangeException(nameof(value), value, "MsmqNonNegativeArgumentExpected"));
        //        }

        //        _maxRetryCycles = value;
        //    }
        //}

        public MsmqTransportSecurity MsmqTransportSecurity { get; internal set; }

        public bool ReceiveContextEnabled { get; set; }

        ///// <summary>Gets or sets an enumeration value that specifies how poison and other messages that cannot be dispatched are handled.</summary>
        ///// <returns>A <see cref="T:System.ServiceModel.ReceiveErrorHandling" /> value that specifies how poison and other messages that cannot be dispatched are handled.</returns>
        ///// <exception cref="T:System.ArgumentOutOfRangeException">The value is not within the range of values defined in <see cref="T:System.ServiceModel.ReceiveErrorHandling" />.</exception>
        //public ReceiveErrorHandling ReceiveErrorHandling
        //{
        //    get
        //    {
        //        return receiveErrorHandling;
        //    }
        //    set
        //    {
        //        if (!ReceiveErrorHandlingHelper.IsDefined(value))
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        }
        //        receiveErrorHandling = value;
        //    }
        //}


        public TimeSpan RetryCycleDelay
        {
            get { return _retryCycleDelay; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException());
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException());
                }

                _retryCycleDelay = value;
            }
        }

        public bool UseSourceJournal { get; set; }

        //internal virtual string WsdlTransportUri => null;

        internal MsmqBindingElementBase()
        {
            CustomDeadLetterQueue = null;
            _deadLetterQueue = DeadLetterQueue.System;
            ExactlyOnce = true;
            _maxRetryCycles = 2;
            ReceiveContextEnabled = true;
            //receiveErrorHandling = ReceiveErrorHandling.Fault;
            _receiveRetryCount = 5;
            _retryCycleDelay = MsmqDefaults.RetryCycleDelay;
            _timeToLive = MsmqDefaults.TimeToLive;
            MsmqTransportSecurity = new MsmqTransportSecurity();
            UseSourceJournal = false;
            //ReceiveContextSettings = new MsmqReceiveContextSettings();
        }

        internal MsmqBindingElementBase(MsmqBindingElementBase elementToBeCloned)
            : base(elementToBeCloned)
        {
            CustomDeadLetterQueue = elementToBeCloned.CustomDeadLetterQueue;
            _deadLetterQueue = elementToBeCloned._deadLetterQueue;
            ExactlyOnce = elementToBeCloned.ExactlyOnce;
            _maxRetryCycles = elementToBeCloned._maxRetryCycles;
            //msmqTransportSecurity = new MsmqTransportSecurity(elementToBeCloned.MsmqTransportSecurity);
            ReceiveContextEnabled = elementToBeCloned.ReceiveContextEnabled;
            //receiveErrorHandling = elementToBeCloned.receiveErrorHandling;
            _receiveRetryCount = elementToBeCloned._receiveRetryCount;
            _retryCycleDelay = elementToBeCloned._retryCycleDelay;
            _timeToLive = elementToBeCloned._timeToLive;
            UseSourceJournal = elementToBeCloned.UseSourceJournal;
            //ReceiveContextSettings = elementToBeCloned.ReceiveContextSettings;
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return null;
            }

            return base.GetProperty<T>(context);
        }

        private class BindingDeliveryCapabilitiesHelper : IBindingDeliveryCapabilities
        {
            internal BindingDeliveryCapabilitiesHelper() { }
            bool IBindingDeliveryCapabilities.AssuresOrderedDelivery => false;
            bool IBindingDeliveryCapabilities.QueuedDelivery => true;
        }
    }
}
