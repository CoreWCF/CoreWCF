// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Description
{
    public abstract class TypedMessageConverter
    {
        public static TypedMessageConverter Create(Type messageContract, string action)
        {
            return Create(messageContract, action, null, TypeLoader.DefaultDataContractFormatAttribute);
        }

        public static TypedMessageConverter Create(Type messageContract, string action, string defaultNamespace)
        {
            return Create(messageContract, action, defaultNamespace, TypeLoader.DefaultDataContractFormatAttribute);
        }

        public static TypedMessageConverter Create(Type messageContract, string action, XmlSerializerFormatAttribute formatterAttribute)
        {
            return Create(messageContract, action, null, formatterAttribute);
        }

        public static TypedMessageConverter Create(Type messageContract, string action, DataContractFormatAttribute formatterAttribute)
        {
            return Create(messageContract, action, null, formatterAttribute);
        }

        public static TypedMessageConverter Create(Type messageContract, string action, string defaultNamespace, XmlSerializerFormatAttribute formatterAttribute)
        {
            if (messageContract == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(messageContract)));
            }

            if (defaultNamespace == null)
            {
                defaultNamespace = NamingHelper.DefaultNamespace;
            }

            return new XmlMessageConverter(GetOperationFormatter(messageContract, formatterAttribute, defaultNamespace, action));
        }

        public static TypedMessageConverter Create(Type messageContract, string action, string defaultNamespace, DataContractFormatAttribute formatterAttribute)
        {
            if (messageContract == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(messageContract)));
            }

            if (!messageContract.IsDefined(typeof(MessageContractAttribute), false))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SFxMessageContractAttributeRequired, messageContract), nameof(messageContract)));
            }

            if (defaultNamespace == null)
            {
                defaultNamespace = NamingHelper.DefaultNamespace;
            }

            return new XmlMessageConverter(GetOperationFormatter(messageContract, formatterAttribute, defaultNamespace, action));
        }

        public abstract Message ToMessage(object typedMessage);
        public abstract Message ToMessage(object typedMessage, MessageVersion version);
        public abstract object FromMessage(Message message);

        private static OperationFormatter GetOperationFormatter(Type t, Attribute formatAttribute, string defaultNS, string action)
        {
            bool isXmlSerializer = (formatAttribute is XmlSerializerFormatAttribute);
            TypeLoader<object> typeLoader = new TypeLoader<object>();
            MessageDescription message = typeLoader.CreateTypedMessageDescription(t, null, null, defaultNS, action, MessageDirection.Output);
            ContractDescription contract = new ContractDescription("dummy_contract", defaultNS);
            OperationDescription operation = new OperationDescription(NamingHelper.XmlName(t.Name), contract, false);
            operation.Messages.Add(message);

            if (isXmlSerializer)
            {
                return XmlSerializerOperationBehavior.CreateOperationFormatter(operation, (XmlSerializerFormatAttribute)formatAttribute);
            }
            else
            {
                return new DataContractSerializerOperationFormatter(operation, (DataContractFormatAttribute)formatAttribute, null);
            }
        }
    }

    internal class XmlMessageConverter : TypedMessageConverter
    {
        private OperationFormatter _formatter;

        internal XmlMessageConverter(OperationFormatter formatter)
        {
            _formatter = formatter;
        }

        internal string Action => _formatter.RequestAction;

        public override Message ToMessage(object typedMessage)
        {
            return ToMessage(typedMessage, MessageVersion.Soap12WSAddressing10);
        }

        public override Message ToMessage(object typedMessage, MessageVersion version)
        {
            if (typedMessage == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(typedMessage)));
            }

            return _formatter.SerializeRequest(version, new object[] { typedMessage });
        }

        public override object FromMessage(Message message)
        {
            Fx.Assert(message.Headers != null, "");
            if (Action != null && message.Headers.Action != null && message.Headers.Action != Action)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxActionMismatch, Action, message.Headers.Action)));
            }

            object[] result = new object[1];
            _formatter.DeserializeRequest(message, result);

            return result[0];
        }
    }
}
