using System;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.OperationBehavior)]
    public sealed class OperationBehaviorAttribute : Attribute, IOperationBehavior
    {
        internal const ImpersonationOption DefaultImpersonationOption = ImpersonationOption.NotAllowed;
        ImpersonationOption _impersonation = ImpersonationOption.NotAllowed;
        ReleaseInstanceMode _releaseInstance = ReleaseInstanceMode.None;

        public bool AutoDisposeParameters { get; set; }

        public ImpersonationOption Impersonation
        {
            get
            {
                return _impersonation;
            }
            set
            {
                if (!ImpersonationOptionHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _impersonation = value;
            }
        }

        public ReleaseInstanceMode ReleaseInstanceMode
        {
            get { return _releaseInstance; }
            set
            {
                if (!ReleaseInstanceModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _releaseInstance = value;
            }
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

            if (description.IsServerInitiated() && _releaseInstance != ReleaseInstanceMode.None)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxOperationBehaviorAttributeReleaseInstanceModeDoesNotApplyToCallback,
                    description.Name)));
            }

            dispatch.AutoDisposeParameters = AutoDisposeParameters;
            dispatch.ReleaseInstanceBeforeCall = (_releaseInstance & ReleaseInstanceMode.BeforeCall) != 0;
            dispatch.ReleaseInstanceAfterCall = (_releaseInstance & ReleaseInstanceMode.AfterCall) != 0;
            dispatch.Impersonation = Impersonation;
        }

        void IOperationBehavior.ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
        }
    }
}