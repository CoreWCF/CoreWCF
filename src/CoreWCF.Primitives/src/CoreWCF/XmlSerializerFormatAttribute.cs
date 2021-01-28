// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = false, AllowMultiple = false)]
    public sealed class XmlSerializerFormatAttribute : Attribute
    {
        private OperationFormatStyle _style;
        private bool _isStyleSet;
        private OperationFormatUse _use;

        public bool SupportFaults { get; set; } = false;

        public OperationFormatStyle Style
        {
            get { return _style; }
            set
            {
                ValidateOperationFormatStyle(value);
                _style = value;
                _isStyleSet = true;
            }
        }

        public OperationFormatUse Use
        {
            get { return _use; }
            set
            {
                ValidateOperationFormatUse(value);
                _use = value;
                if (!_isStyleSet && IsEncoded)
                {
                    Style = OperationFormatStyle.Rpc;
                }
            }
        }

        internal bool IsEncoded
        {
            get { return _use == OperationFormatUse.Encoded; }
            set { _use = value ? OperationFormatUse.Encoded : OperationFormatUse.Literal; }
        }

        static internal void ValidateOperationFormatStyle(OperationFormatStyle value)
        {
            if (!OperationFormatStyleHelper.IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
            }
        }

        static internal void ValidateOperationFormatUse(OperationFormatUse value)
        {
            if (!OperationFormatUseHelper.IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
            }
        }
    }
}