﻿using System;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF
{
    public sealed class OperationBehaviorAttribute : Attribute, IOperationBehavior
    {
        internal const ImpersonationOption DefaultImpersonationOption = ImpersonationOption.NotAllowed;
        private bool _autoDisposeParameters;

        public bool AutoDisposeParameters
        {
            get { return _autoDisposeParameters; }
            set { _autoDisposeParameters = value; }
        }

        void IOperationBehavior.AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters) { }
        void IOperationBehavior.Validate(OperationDescription operationDescription) { }

        void IOperationBehavior.ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }
            if (dispatch == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatch));
            }
            //if (description.IsServerInitiated() && this.releaseInstance != ReleaseInstanceMode.None)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
            //        SR.Format(SR.SFxOperationBehaviorAttributeReleaseInstanceModeDoesNotApplyToCallback,
            //        description.Name)));
            //}
            //dispatch.TransactionRequired = this.autoEnlistTransaction;
            //dispatch.TransactionAutoComplete = this.autoCompleteTransaction;
            dispatch.AutoDisposeParameters = _autoDisposeParameters;
            //dispatch.ReleaseInstanceBeforeCall = (this.releaseInstance & ReleaseInstanceMode.BeforeCall) != 0;
            //dispatch.ReleaseInstanceAfterCall = (this.releaseInstance & ReleaseInstanceMode.AfterCall) != 0;
            //dispatch.Impersonation = this.Impersonation;
        }

        void IOperationBehavior.ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
        }
    }
}