// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    internal sealed class ImpersonatingMessage : Message
    {
        private readonly Message _innerMessage;

        public ImpersonatingMessage(Message innerMessage)
        {
            _innerMessage = innerMessage ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerMessage));
        }

        public override bool IsEmpty
        {
            get
            {
                return _innerMessage.IsEmpty;
            }
        }

        public override bool IsFault
        {
            get { return _innerMessage.IsFault; }
        }

        public override MessageHeaders Headers
        {
            get { return _innerMessage.Headers; }
        }

        public override MessageProperties Properties
        {
            get { return _innerMessage.Properties; }
        }

        public override MessageVersion Version
        {
            get { return _innerMessage.Version; }
        }

        public override RecycledMessageState RecycledMessageState
        {
            get
            {
                return _innerMessage.RecycledMessageState;
            }
        }

        protected override void OnClose()
        {
            base.OnClose();
            _innerMessage.Close();
        }

        //Runs impersonated.
        public override Task OnWriteMessageAsync(XmlDictionaryWriter writer)
        {
            return ImpersonateCall(() => _innerMessage.WriteMessageAsync(writer));
        }

        //Runs impersonated.
        protected override void OnWriteMessage(XmlDictionaryWriter writer)
        {
            ImpersonateCall(() => _innerMessage.WriteMessage(writer));
        }

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            _innerMessage.WriteStartEnvelope(writer);
        }

        protected override void OnWriteStartHeaders(XmlDictionaryWriter writer)
        {
            _innerMessage.WriteStartHeaders(writer);
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            _innerMessage.WriteStartBody(writer);
        }

        protected override string OnGetBodyAttribute(string localName, string ns)
        {
            return _innerMessage.GetBodyAttribute(localName, ns);
        }

        protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
        {
            return _innerMessage.CreateBufferedCopy(maxBufferSize);
        }

        internal override Task OnWriteBodyContentsAsync(XmlDictionaryWriter writer)
        {
            return ImpersonateCall(() => _innerMessage.WriteBodyContentsAsync(writer));
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            ImpersonateCall(() => _innerMessage.WriteBodyContents(writer));
        }

        //Runs impersonated.
        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            ImpersonateCall(() => _innerMessage.BodyToString(writer));
        }

        private void ImpersonateCall(Action callToImpersonate)
        {
            if (ImpersonateOnSerializingReplyMessageProperty.TryGet(_innerMessage, out ImpersonateOnSerializingReplyMessageProperty impersonationProperty))
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
            if (ImpersonateOnSerializingReplyMessageProperty.TryGet(_innerMessage, out ImpersonateOnSerializingReplyMessageProperty impersonationProperty))
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
