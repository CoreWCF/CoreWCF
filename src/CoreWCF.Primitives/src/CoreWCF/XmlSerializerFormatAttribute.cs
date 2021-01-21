// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = false, AllowMultiple = false)]
    public sealed class XmlSerializerFormatAttribute : Attribute
    {
        private bool supportFaults = false;
        private OperationFormatStyle style;
        private bool isStyleSet;
        private OperationFormatUse use;

        public bool SupportFaults
        {
            get { return supportFaults; }
            set { supportFaults = value; }
        }

        public OperationFormatStyle Style
        {
            get { return style; }
            set
            {
                ValidateOperationFormatStyle(value);
                style = value;
                isStyleSet = true;
            }
        }

        public OperationFormatUse Use
        {
            get { return use; }
            set
            {
                ValidateOperationFormatUse(value);
                use = value;
                if (!isStyleSet && IsEncoded)
                    Style = OperationFormatStyle.Rpc;
            }
        }

        internal bool IsEncoded
        {
            get { return use == OperationFormatUse.Encoded; }
            set { use = value ? OperationFormatUse.Encoded : OperationFormatUse.Literal; }
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