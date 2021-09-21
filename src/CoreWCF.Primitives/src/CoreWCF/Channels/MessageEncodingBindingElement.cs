// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public abstract class MessageEncodingBindingElement : BindingElement
    {
        protected MessageEncodingBindingElement()
        {
        }

        protected MessageEncodingBindingElement(MessageEncodingBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
        }

        public abstract MessageVersion MessageVersion { get; set; }

        public abstract MessageEncoderFactory CreateMessageEncoderFactory();

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(MessageVersion))
            {
                return (T)(object)MessageVersion;
            }
            else
            {
                return context.GetInnerProperty<T>();
            }
        }

        internal virtual bool CheckEncodingVersion(EnvelopeVersion version)
        {
            return false;
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (!(b is MessageEncodingBindingElement encoding))
            {
                return false;
            }

            return true;
        }
    }
}
