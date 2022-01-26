// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class WebFaultFormatter : IDispatchFaultFormatter, IDispatchFaultFormatterWrapper
    {
        private static MessageFault s_defaultMessageFault;

        internal WebFaultFormatter(IDispatchFaultFormatter faultFormatter)
        {
            InnerFaultFormatter = faultFormatter;
        }

        public MessageFault Serialize(FaultException faultException, out string action)
        {
            try
            {
                return InnerFaultFormatter.Serialize(faultException, out action);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                action = null;
                return Default;
            }
        }

        public IDispatchFaultFormatter InnerFaultFormatter { get; set; }

        private static MessageFault Default
        {
            get
            {
                if (s_defaultMessageFault == null)
                {
                    s_defaultMessageFault = MessageFault.CreateFault(
                        new FaultCode("Default"),
                        new FaultReason(""),
                        null,
                        null,
                        "",
                        "");
                }

                return s_defaultMessageFault;
            }
        }
    }
}
