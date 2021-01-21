// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Channels
{
    internal abstract class DelegatingMessage : Message
    {
        private readonly Message innerMessage;

        protected DelegatingMessage(Message innerMessage)
        {
            if (innerMessage == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("innerMessage");
            }
            this.innerMessage = innerMessage;
        }

        public override bool IsEmpty
        {
            get
            {
                return innerMessage.IsEmpty;
            }
        }

        public override bool IsFault
        {
            get { return innerMessage.IsFault; }
        }

        public override MessageHeaders Headers
        {
            get { return innerMessage.Headers; }
        }

        public override MessageProperties Properties
        {
            get { return innerMessage.Properties; }
        }

        public override MessageVersion Version
        {
            get { return innerMessage.Version; }
        }

        protected Message InnerMessage
        {
            get { return innerMessage; }
        }

        protected override void OnClose()
        {
            base.OnClose();
            innerMessage.Close();
        }

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            innerMessage.WriteStartEnvelope(writer);
        }

        protected override void OnWriteStartHeaders(XmlDictionaryWriter writer)
        {
            innerMessage.WriteStartHeaders(writer);
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            innerMessage.WriteStartBody(writer);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            innerMessage.WriteBodyContents(writer);
        }

        protected override string OnGetBodyAttribute(string localName, string ns)
        {
            return innerMessage.GetBodyAttribute(localName, ns);
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            innerMessage.BodyToString(writer);
        }
    }
}
