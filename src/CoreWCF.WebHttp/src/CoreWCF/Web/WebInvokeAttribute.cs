// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF.Web
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WebInvokeAttribute : Attribute, IOperationContractAttributeProvider, IOperationBehavior
    {
        private WebMessageBodyStyle _bodyStyle;
        private WebMessageFormat _requestMessageFormat;
        private WebMessageFormat _responseMessageFormat;

        public WebInvokeAttribute()
        {
        }

        public WebMessageBodyStyle BodyStyle
        {
            get { return _bodyStyle; }
            set
            {
                if (!WebMessageBodyStyleHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _bodyStyle = value;
                IsBodyStyleSetExplicitly = true;
            }
        }

        public bool IsBodyStyleSetExplicitly { get; private set; }

        public bool IsRequestFormatSetExplicitly { get; private set; }

        public bool IsResponseFormatSetExplicitly { get; private set; }

        public string Method { get; set; }

        public WebMessageFormat RequestFormat
        {
            get
            {

                return _requestMessageFormat;
            }
            set
            {
                if (!WebMessageFormatHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _requestMessageFormat = value;
                IsRequestFormatSetExplicitly = true;
            }
        }

        public WebMessageFormat ResponseFormat
        {
            get
            {

                return _responseMessageFormat;
            }
            set
            {
                if (!WebMessageFormatHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _responseMessageFormat = value;
                IsResponseFormatSetExplicitly = true;
            }
        }

        public string UriTemplate { get; set; }

        void IOperationBehavior.AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters)
        {
        } //  do nothing

        void IOperationBehavior.ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation)
        {
        } //  do nothing

        void IOperationBehavior.ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
        {
        } //  do nothing

        void IOperationBehavior.Validate(OperationDescription operationDescription)
        {
        } //  do nothing 

        internal WebMessageBodyStyle GetBodyStyleOrDefault(WebMessageBodyStyle defaultStyle)
        {
            if (IsBodyStyleSetExplicitly)
            {
                return BodyStyle;
            }
            else
            {
                return defaultStyle;
            }
        }

        OperationContractAttribute IOperationContractAttributeProvider.GetOperationContractAttribute() => new OperationContractAttribute();
    }
}
