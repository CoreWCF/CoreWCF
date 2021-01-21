// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;

namespace CoreWCF
{
    public class FaultCode
    {
        FaultCode subCode;
        string name;
        string ns;
        EnvelopeVersion version;

        public FaultCode(string name)
            : this(name, "", null)
        {
        }

        public FaultCode(string name, FaultCode subCode)
            : this(name, "", subCode)
        {
        }

        public FaultCode(string name, string ns)
            : this(name, ns, null)
        {
        }

        public FaultCode(string name, string ns, FaultCode subCode)
        {
            if (name == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            if (name.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(name)));

            if (!string.IsNullOrEmpty(ns))
                NamingHelper.CheckUriParameter(ns, "ns");

            this.name = name;
            this.ns = ns;
            this.subCode = subCode;

            if (ns == Message12Strings.Namespace)
                version = EnvelopeVersion.Soap12;
            else if (ns == Message11Strings.Namespace)
                version = EnvelopeVersion.Soap11;
            else if (ns == MessageStrings.Namespace)
                version = EnvelopeVersion.None;
            else
                version = null;
        }

        public bool IsPredefinedFault
        {
            get
            {
                return ns.Length == 0 || version != null;
            }
        }

        public bool IsSenderFault
        {
            get
            {
                if (IsPredefinedFault)
                    return name == (version ?? EnvelopeVersion.Soap12).SenderFaultName;

                return false;
            }
        }

        public bool IsReceiverFault
        {
            get
            {
                if (IsPredefinedFault)
                    return name == (version ?? EnvelopeVersion.Soap12).ReceiverFaultName;

                return false;
            }
        }

        public string Namespace
        {
            get
            {
                return ns;
            }
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public FaultCode SubCode
        {
            get
            {
                return subCode;
            }
        }

        public static FaultCode CreateSenderFaultCode(FaultCode subCode)
        {
            return new FaultCode("Sender", subCode);
        }

        // TODO: Consider making this public
        internal static FaultCode CreateSenderFaultCode(string name, string ns)
        {
            return CreateSenderFaultCode(new FaultCode(name, ns));
        }

        internal static FaultCode CreateReceiverFaultCode(FaultCode subCode)
        {
            if (subCode == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(subCode));
            return new FaultCode("Receiver", subCode);
        }
    }
}