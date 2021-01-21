// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Diagnostics;

namespace CoreWCF.Channels
{
    /// <summary>
    /// Base class for non-SOAP messages
    /// </summary>
    internal abstract class ContentOnlyMessage : Message
    {
        private readonly MessageHeaders headers;
        private MessageProperties properties;

        protected ContentOnlyMessage()
        {
            headers = new MessageHeaders(MessageVersion.None);
        }

        public override MessageHeaders Headers
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(CreateMessageDisposedException(), this);
                }

                return headers;
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

                if (properties == null)
                {
                    properties = new MessageProperties();
                }

                return properties;
            }
        }

        public override MessageVersion Version
        {
            get
            {
                return headers.MessageVersion;
            }
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            OnWriteBodyContents(writer);
        }
    }

    internal class StringMessage : ContentOnlyMessage
    {
        private readonly string data;

        public StringMessage(string data)
            : base()
        {
            this.data = data;
        }

        public override bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(data);
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            if (data != null && data.Length > 0)
            {
                writer.WriteElementString("BODY", data);
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