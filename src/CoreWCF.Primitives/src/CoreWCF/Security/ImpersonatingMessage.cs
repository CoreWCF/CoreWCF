// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    sealed class ImpersonatingMessage : Message
    {
        Message innerMessage;

        public ImpersonatingMessage(Message innerMessage)
        {
            if (innerMessage == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerMessage));
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

        internal override RecycledMessageState RecycledMessageState
        {
            get
            {
                return innerMessage.RecycledMessageState;
            }
        }

        protected override void OnClose()
        {
            base.OnClose();
            innerMessage.Close();
        }

        //Runs impersonated.
        public override Task OnWriteMessageAsync(XmlDictionaryWriter writer)
        {
            return ImpersonateCall(() => innerMessage.WriteMessageAsync(writer));
        }

        //Runs impersonated.
        protected override void OnWriteMessage(XmlDictionaryWriter writer)
        {
            ImpersonateCall(() => innerMessage.WriteMessage(writer));
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

        protected override string OnGetBodyAttribute(string localName, string ns)
        {
            return innerMessage.GetBodyAttribute(localName, ns);
        }

        protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
        {
            return innerMessage.CreateBufferedCopy(maxBufferSize);
        }

        internal override Task OnWriteBodyContentsAsync(XmlDictionaryWriter writer)
        {
            return ImpersonateCall(() => innerMessage.WriteBodyContentsAsync(writer));
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            ImpersonateCall(() => innerMessage.WriteBodyContents(writer));
        }

        //Runs impersonated.
        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            ImpersonateCall(() => innerMessage.BodyToString(writer));
        }

        private void ImpersonateCall(Action callToImpersonate)
        {
            if (ImpersonateOnSerializingReplyMessageProperty.TryGet(innerMessage, out ImpersonateOnSerializingReplyMessageProperty impersonationProperty))
            {
                _ = impersonationProperty.RunImpersonated(() =>
                      {
                          callToImpersonate();
                          return (object)null;
                      });
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.UnableToImpersonateWhileSerializingReponse));
            }
        }

        private T ImpersonateCall<T>(Func<T> callToImpersonate)
        {
            if (ImpersonateOnSerializingReplyMessageProperty.TryGet(innerMessage, out ImpersonateOnSerializingReplyMessageProperty impersonationProperty))
            {
                return impersonationProperty.RunImpersonated(callToImpersonate);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.UnableToImpersonateWhileSerializingReponse));

            }
        }
    }
}
