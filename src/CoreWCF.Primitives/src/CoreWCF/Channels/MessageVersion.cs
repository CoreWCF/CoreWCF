// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class MessageVersion
    {
        private MessageVersion(EnvelopeVersion envelopeVersion, AddressingVersion addressingVersion)
        {
            Envelope = envelopeVersion;
            Addressing = addressingVersion;
        }

        public AddressingVersion Addressing { get; }
        public EnvelopeVersion Envelope { get; }

        public static MessageVersion Default => Soap12WSAddressing10;
        public static MessageVersion None { get; } = new MessageVersion(EnvelopeVersion.None, AddressingVersion.None);
        public static MessageVersion Soap11 { get; } = new MessageVersion(EnvelopeVersion.Soap11, AddressingVersion.None);
        public static MessageVersion Soap12 { get; } = new MessageVersion(EnvelopeVersion.Soap12, AddressingVersion.None);
        public static MessageVersion Soap11WSAddressing10 { get; } = new MessageVersion(EnvelopeVersion.Soap11, AddressingVersion.WSAddressing10);
        public static MessageVersion Soap12WSAddressing10 { get; } = new MessageVersion(EnvelopeVersion.Soap12, AddressingVersion.WSAddressing10);
        public static MessageVersion Soap11WSAddressingAugust2004 { get; } = new MessageVersion(EnvelopeVersion.Soap11, AddressingVersion.WSAddressingAugust2004);
        public static MessageVersion Soap12WSAddressingAugust2004 { get; } = new MessageVersion(EnvelopeVersion.Soap12, AddressingVersion.WSAddressingAugust2004);

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
                    return Soap12WSAddressing10;
                }
                else if (addressingVersion == AddressingVersion.WSAddressingAugust2004)
                {
                    return Soap12WSAddressingAugust2004;
                }
                else if (addressingVersion == AddressingVersion.None)
                {
                    return Soap12;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(addressingVersion),
                        SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
                }
            }
            else if (envelopeVersion == EnvelopeVersion.Soap11)
            {
                if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    return Soap11WSAddressing10;
                }
                else if (addressingVersion == AddressingVersion.WSAddressingAugust2004)
                {
                    return Soap11WSAddressingAugust2004;
                }
                else if (addressingVersion == AddressingVersion.None)
                {
                    return Soap11;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(addressingVersion),
                        SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
                }
            }
            else if (envelopeVersion == EnvelopeVersion.None)
            {
                if (addressingVersion == AddressingVersion.None)
                {
                    return None;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(addressingVersion),
                        SR.Format(SR.AddressingVersionNotSupported, addressingVersion));
                }
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(envelopeVersion),
                    SR.Format(SR.EnvelopeVersionNotSupported, envelopeVersion));
            }
        }

        public override bool Equals(object obj)
        {
            return this == obj;
        }

        public override int GetHashCode()
        {
            int code = 0;
            if (Envelope == EnvelopeVersion.Soap11)
            {
                code += 1;
            }

            return code;
        }

        public override string ToString()
        {
            return SR.Format(SR.MessageVersionToStringFormat, Envelope.ToString(), Addressing.ToString());
        }

        internal bool IsMatch(MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                Fx.Assert("Invalid (null) messageVersion value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }
            if (Addressing == null)
            {
                Fx.Assert("Invalid (null) addressing value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "MessageVersion.Addressing cannot be null")));
            }

            if (Envelope != messageVersion.Envelope)
            {
                return false;
            }

            if (Addressing.Namespace != messageVersion.Addressing.Namespace)
            {
                return false;
            }

            return true;
        }
    }
}
