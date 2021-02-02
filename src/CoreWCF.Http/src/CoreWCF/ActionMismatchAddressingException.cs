// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    [Serializable]
    internal class ActionMismatchAddressingException : ProtocolException
    {
        private readonly string _httpActionHeader;
        private readonly string _soapActionHeader;

        public ActionMismatchAddressingException(string message, string soapActionHeader, string httpActionHeader)
            : base(message)
        {
            _httpActionHeader = httpActionHeader;
            _soapActionHeader = soapActionHeader;
        }

        protected ActionMismatchAddressingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public string HttpActionHeader
        {
            get
            {
                return _httpActionHeader;
            }
        }

        public string SoapActionHeader
        {
            get
            {
                return _soapActionHeader;
            }
        }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            Fx.Assert(messageVersion.Addressing == AddressingVersion.WSAddressing10, "");
            WSAddressing10ProblemHeaderQNameFault phf = new WSAddressing10ProblemHeaderQNameFault(this);
            Message message = Channels.Message.CreateMessage(messageVersion, phf, messageVersion.Addressing.FaultAction);
            phf.AddHeaders(message.Headers);
            return message;
        }
    }
}
