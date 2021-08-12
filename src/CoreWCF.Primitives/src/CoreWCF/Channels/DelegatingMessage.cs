// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Channels
{
    internal abstract class DelegatingMessage : Message
    {
        protected DelegatingMessage(Message innerMessage)
        {
            InnerMessage = innerMessage ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerMessage));
        }

        public override bool IsEmpty
        {
            get
            {
                return InnerMessage.IsEmpty;
            }
        }

        public override bool IsFault
        {
            get { return InnerMessage.IsFault; }
        }

        public override MessageHeaders Headers
        {
            get { return InnerMessage.Headers; }
        }

        public override MessageProperties Properties
        {
            get { return InnerMessage.Properties; }
        }

        public override MessageVersion Version
        {
            get { return InnerMessage.Version; }
        }

        protected Message InnerMessage { get; }

        protected override void OnClose()
        {
            base.OnClose();
            InnerMessage.Close();
        }

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            InnerMessage.WriteStartEnvelope(writer);
        }

        protected override void OnWriteStartHeaders(XmlDictionaryWriter writer)
        {
            InnerMessage.WriteStartHeaders(writer);
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            InnerMessage.WriteStartBody(writer);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            InnerMessage.WriteBodyContents(writer);
        }

        protected override string OnGetBodyAttribute(string localName, string ns)
        {
            return InnerMessage.GetBodyAttribute(localName, ns);
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            InnerMessage.BodyToString(writer);
        }
    }
}
