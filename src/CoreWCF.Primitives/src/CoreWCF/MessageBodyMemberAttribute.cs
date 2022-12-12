// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, Inherited = false)]
    public class MessageBodyMemberAttribute : MessageContractMemberAttribute
    {
        private int _order = -1;
        internal const string OrderPropertyName = "Order";
        public int Order
        {
            get { return _order; }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SRCommon.ValueMustBeNonNegative));
                }

                _order = value;
            }
        }
    }
}