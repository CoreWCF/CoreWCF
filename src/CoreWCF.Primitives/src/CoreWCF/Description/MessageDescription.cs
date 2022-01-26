// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class MessageDescription
    {
        private static Type s_typeOfUntypedMessage;
        private MessageDescriptionItems _items;

        //XmlQualifiedName xsdType;

        public MessageDescription(string action, MessageDirection direction) : this(action, direction, null) { }

        internal MessageDescription(string action, MessageDirection direction, MessageDescriptionItems items)
        {
            if (!MessageDirectionHelper.IsDefined(direction))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(direction)));
            }

            Action = action;
            Direction = direction;
            _items = items;
        }

        internal MessageDescription(MessageDescription other)
        {
            Action = other.Action;
            Direction = other.Direction;
            Items.Body = other.Items.Body.Clone();
            foreach (MessageHeaderDescription mhd in other.Items.Headers)
            {
                Items.Headers.Add(mhd.Clone() as MessageHeaderDescription);
            }
            foreach (MessagePropertyDescription mpd in other.Items.Properties)
            {
                Items.Properties.Add(mpd.Clone() as MessagePropertyDescription);
            }
            MessageName = other.MessageName;
            MessageType = other.MessageType;

        }

        public MessageDescription Clone()
        {
            return new MessageDescription(this);
        }

        public string Action { get; internal set; }

        public MessageBodyDescription Body
        {
            get { return Items.Body; }
        }

        public MessageDirection Direction { get; }

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
                if (_items == null)
                {
                    _items = new MessageDescriptionItems();
                }

                return _items;
            }
        }

        internal bool HasProtectionLevel => false;

        internal static Type TypeOfUntypedMessage
        {
            get
            {
                if (s_typeOfUntypedMessage == null)
                {
                    s_typeOfUntypedMessage = typeof(Message);
                }
                return s_typeOfUntypedMessage;
            }
        }

        internal XmlName MessageName { get; set; }

        public Type MessageType { get; set; }

        internal bool IsTypedMessage
        {
            get
            {
                return MessageType != null;
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
        private MessageHeaderDescriptionCollection _headers;
        private MessageBodyDescription _body;
        private MessagePropertyDescriptionCollection _properties;

        internal MessageBodyDescription Body
        {
            get
            {
                if (_body == null)
                {
                    _body = new MessageBodyDescription();
                }

                return _body;
            }
            set
            {
                _body = value;
            }
        }

        internal MessageHeaderDescriptionCollection Headers
        {
            get
            {
                if (_headers == null)
                {
                    _headers = new MessageHeaderDescriptionCollection();
                }

                return _headers;
            }
        }

        internal MessagePropertyDescriptionCollection Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = new MessagePropertyDescriptionCollection();
                }

                return _properties;
            }
        }
    }
}
