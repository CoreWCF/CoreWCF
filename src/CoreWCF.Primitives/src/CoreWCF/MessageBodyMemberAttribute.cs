using System;

namespace CoreWCF
{
    // TODO: Make this public
    [AttributeUsage(ServiceModelAttributeTargets.MessageMember, Inherited = false)]
    public class MessageBodyMemberAttribute : MessageContractMemberAttribute
    {
        int _order = -1;
        internal const string OrderPropertyName = "Order";
        public int Order
        {
            get { return _order; }
            set
            {
                if (value < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.ValueMustBeNonNegative));

                _order = value;
            }
        }

    }
}