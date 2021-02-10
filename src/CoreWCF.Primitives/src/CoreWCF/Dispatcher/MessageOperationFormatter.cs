// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal sealed class MessageOperationFormatter : IClientMessageFormatter, IDispatchMessageFormatter
    {
        private static MessageOperationFormatter s_instance;

        internal static MessageOperationFormatter Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new MessageOperationFormatter();
                }

                return s_instance;
            }
        }

        public object DeserializeReply(Message message, object[] parameters)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (parameters != null && parameters.Length > 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxParametersMustBeEmpty);
            }

            return message;
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (parameters == null)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)), message);
            }

            if (parameters.Length != 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxParameterMustBeArrayOfOneElement);
            }

            parameters[0] = message;
        }

        public bool IsFault(string operation, Exception error)
        {
            return false;
        }

        public MessageFault SerializeFault(Exception error)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxMessageOperationFormatterCannotSerializeFault));
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            if (!(result is Message))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxResultMustBeMessage);
            }

            if (parameters != null && parameters.Length > 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxParametersMustBeEmpty);
            }

            return (Message)result;
        }

        public Message SerializeRequest(MessageVersion messageVersion, object[] parameters)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            if (parameters.Length != 1 || !(parameters[0] is Message))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxParameterMustBeMessage);
            }

            return (Message)parameters[0];
        }
    }
}