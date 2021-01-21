// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class MessageDescription
    {
        private static Type typeOfUntypedMessage;
        private string action;
        private MessageDirection direction;
        private MessageDescriptionItems items;
        private XmlName messageName;
        private Type messageType;
        //XmlQualifiedName xsdType;

        public MessageDescription(string action, MessageDirection direction) : this(action, direction, null) { }

        internal MessageDescription(string action, MessageDirection direction, MessageDescriptionItems items)
        {
            if (!MessageDirectionHelper.IsDefined(direction))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(direction)));

            this.action = action;
            this.direction = direction;
            this.items = items;
        }

        public string Action
        {
            get { return action; }
            internal set { action = value; }
        }

        public MessageBodyDescription Body
        {
            get { return Items.Body; }
        }

        public MessageDirection Direction
        {
            get { return direction; }
        }

        public MessageHeaderDescriptionCollection Headers
        {
            get { return Items.Headers; }
        }

        public MessagePropertyDescriptionCollection Properties
        {
            get { return Items.Properties; }
        }

        internal MessageDescriptionItems Items
        {
            get
            {
                if (items == null)
                    items = new MessageDescriptionItems();
                return items;
            }
        }

        internal bool HasProtectionLevel => false;

        internal static Type TypeOfUntypedMessage
        {
            get
            {
                if (typeOfUntypedMessage == null)
                {
                    typeOfUntypedMessage = typeof(Message);
                }
                return typeOfUntypedMessage;
            }
        }

        internal XmlName MessageName
        {
            get { return messageName; }
            set { messageName = value; }
        }

        public Type MessageType
        {
            get { return messageType; }
            set { messageType = value; }
        }

        internal bool IsTypedMessage
        {
            get
            {
                return messageType != null;
            }
        }

        internal bool IsUntypedMessage
        {
            get
            {
                return (Body.ReturnValue != null && Body.Parts.Count == 0 && Body.ReturnValue.Type == TypeOfUntypedMessage) ||
                     (Body.ReturnValue == null && Body.Parts.Count == 1 && Body.Parts[0].Type == TypeOfUntypedMessage);
            }
        }

        internal bool IsVoid
        {
            get
            {
                return !IsTypedMessage && Body.Parts.Count == 0 && (Body.ReturnValue == null || Body.ReturnValue.Type == typeof(void));
            }
        }
    }

    internal class MessageDescriptionItems
    {
        private MessageHeaderDescriptionCollection headers;
        private MessageBodyDescription body;
        private MessagePropertyDescriptionCollection properties;

        internal MessageBodyDescription Body
        {
            get
            {
                if (body == null)
                    body = new MessageBodyDescription();
                return body;
            }
            set
            {
                body = value;
            }
        }

        internal MessageHeaderDescriptionCollection Headers
        {
            get
            {
                if (headers == null)
                    headers = new MessageHeaderDescriptionCollection();
                return headers;
            }
        }

        internal MessagePropertyDescriptionCollection Properties
        {
            get
            {
                if (properties == null)
                    properties = new MessagePropertyDescriptionCollection();
                return properties;
            }
        }
    }

}