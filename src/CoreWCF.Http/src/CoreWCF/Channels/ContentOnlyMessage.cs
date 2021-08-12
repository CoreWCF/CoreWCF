// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using CoreWCF.Diagnostics;

// TODO: This is duplicated from Primitives. Either move to common code and include in both places or add to contract. I would prefer the latter.

namespace CoreWCF.Channels
{
    /// <summary>
    /// Base class for non-SOAP messages
    /// </summary>
    internal abstract class ContentOnlyMessage : Message
    {
        private readonly MessageHeaders _headers;
        private MessageProperties _properties;

        protected ContentOnlyMessage()
        {
            _headers = new MessageHeaders(MessageVersion.None);
        }

        public override MessageHeaders Headers
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(CreateMessageDisposedException(), this);
                }

                return _headers;
            }
        }

        public override MessageProperties Properties
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(CreateMessageDisposedException(), this);
                }

                if (_properties == null)
                {
                    _properties = new MessageProperties();
                }

                return _properties;
            }
        }

        public override MessageVersion Version
        {
            get
            {
                return _headers.MessageVersion;
            }
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            OnWriteBodyContents(writer);
        }

        internal Exception CreateMessageDisposedException()
        {
            return new ObjectDisposedException("", SR.MessageClosed);
        }
    }

    internal class StringMessage : ContentOnlyMessage
    {
        private readonly string _data;

        public StringMessage(string data)
            : base()
        {
            _data = data;
        }

        public override bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(_data);
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            if (_data != null && _data.Length > 0)
            {
                writer.WriteElementString("BODY", _data);
            }
        }
    }

    internal class NullMessage : StringMessage
    {
        public NullMessage()
            : base(string.Empty)
        {
        }
    }
}