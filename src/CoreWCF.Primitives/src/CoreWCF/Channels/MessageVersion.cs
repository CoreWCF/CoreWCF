using System;
using System.Globalization;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class MessageVersion
    {
        EnvelopeVersion envelope;
        AddressingVersion addressing;
        static MessageVersion none;
        static MessageVersion soap11;
        static MessageVersion soap12Addressing10;

        static MessageVersion()
        {
            none = new MessageVersion(EnvelopeVersion.None, AddressingVersion.None);
            soap11 = new MessageVersion(EnvelopeVersion.Soap11, AddressingVersion.None);
            soap12Addressing10 = new MessageVersion(EnvelopeVersion.Soap12, AddressingVersion.WSAddressing10);
        }

        private MessageVersion(EnvelopeVersion envelopeVersion, AddressingVersion addressingVersion)
        {
            envelope = envelopeVersion;
            addressing = addressingVersion;
        }

        public static MessageVersion CreateVersion(EnvelopeVersion envelopeVersion)
        {
            return CreateVersion(envelopeVersion, AddressingVersion.WSAddressing10);
        }

        public static MessageVersion CreateVersion(EnvelopeVersion envelopeVersion, AddressingVersion addressingVersion)
        {
            if (envelopeVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(envelopeVersion));
            }

            if (addressingVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(addressingVersion));
            }

            if (envelopeVersion == EnvelopeVersion.Soap12)
            {
                if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    return soap12Addressing10;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("addressingVersion",
                        SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
                }
            }
            else if (envelopeVersion == EnvelopeVersion.Soap11)
            {
                if (addressingVersion == AddressingVersion.None)
                {
                    return soap11;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("addressingVersion",
                        SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
                }
            }
            else if (envelopeVersion == EnvelopeVersion.None)
            {
                if (addressingVersion == AddressingVersion.None)
                {
                    return none;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("addressingVersion",
                        SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
                }
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("envelopeVersion",
                    SR.Format(SR.EnvelopeVersionNotSupported, envelopeVersion));
            }
        }

        public AddressingVersion Addressing
        {
            get { return addressing; }
        }

        public static MessageVersion Default
        {
            get { return soap12Addressing10; }
        }

        public EnvelopeVersion Envelope
        {
            get { return envelope; }
        }

        public override bool Equals(object obj)
        {
            return this == obj;
        }

        public override int GetHashCode()
        {
            int code = 0;
            if (Envelope == EnvelopeVersion.Soap11)
                code += 1;
            return code;
        }

        public static MessageVersion None
        {
            get { return none; }
        }

        public static MessageVersion Soap12WSAddressing10
        {
            get { return soap12Addressing10; }
        }

        public static MessageVersion Soap11
        {
            get { return soap11; }
        }

        public override string ToString()
        {
            return SR.Format(SR.MessageVersionToStringFormat, envelope.ToString(), addressing.ToString());
        }

        internal bool IsMatch(MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                Fx.Assert("Invalid (null) messageVersion value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }
            if (addressing == null)
            {
                Fx.Assert("Invalid (null) addressing value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "MessageVersion.Addressing cannot be null")));
            }

            if (envelope != messageVersion.Envelope)
                return false;
            if (addressing.Namespace != messageVersion.Addressing.Namespace)
                return false;
            return true;
        }
    }
}