// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class MessageVersion
    {
        private readonly AddressingVersion _addressing;
        private static readonly MessageVersion s_soap12Addressing10;

        static MessageVersion()
        {
            None = new MessageVersion(EnvelopeVersion.None, AddressingVersion.None);
            Soap11 = new MessageVersion(EnvelopeVersion.Soap11, AddressingVersion.None);
            s_soap12Addressing10 = new MessageVersion(EnvelopeVersion.Soap12, AddressingVersion.WSAddressing10);
        }

        private MessageVersion(EnvelopeVersion envelopeVersion, AddressingVersion addressingVersion)
        {
            Envelope = envelopeVersion;
            _addressing = addressingVersion;
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
                    return s_soap12Addressing10;
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
                    return Soap11;
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
                    return None;
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
            get { return _addressing; }
        }

        public static MessageVersion Default
        {
            get { return s_soap12Addressing10; }
        }

        public EnvelopeVersion Envelope { get; }

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

        public static MessageVersion None { get; private set; }

        public static MessageVersion Soap12WSAddressing10
        {
            get { return s_soap12Addressing10; }
        }

        public static MessageVersion Soap11 { get; private set; }

        public override string ToString()
        {
            return SR.Format(SR.MessageVersionToStringFormat, Envelope.ToString(), _addressing.ToString());
        }

        internal bool IsMatch(MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                Fx.Assert("Invalid (null) messageVersion value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }
            if (_addressing == null)
            {
                Fx.Assert("Invalid (null) addressing value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "MessageVersion.Addressing cannot be null")));
            }

            if (Envelope != messageVersion.Envelope)
            {
                return false;
            }

            if (_addressing.Namespace != messageVersion.Addressing.Namespace)
            {
                return false;
            }

            return true;
        }
    }
}