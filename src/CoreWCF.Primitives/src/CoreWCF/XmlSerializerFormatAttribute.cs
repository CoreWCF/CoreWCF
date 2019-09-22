using System;

namespace CoreWCF
{
    public sealed class XmlSerializerFormatAttribute : Attribute
    {
        bool supportFaults = false;
        OperationFormatStyle style;
        bool isStyleSet;
        OperationFormatUse use;

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