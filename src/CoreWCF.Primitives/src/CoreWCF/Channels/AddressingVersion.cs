// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.Channels
{
    public sealed class AddressingVersion
    {
        /*MessagePartSpecification signedMessageParts;*/
        string toStringFormat;

        AddressingVersion(string ns, XmlDictionaryString dictionaryNs, string toStringFormat,
            /*MessagePartSpecification signedMessageParts,*/ string anonymous, XmlDictionaryString dictionaryAnonymous, string none, string faultAction, string defaultFaultAction)
        {
            Namespace = ns;
            DictionaryNamespace = dictionaryNs;
            this.toStringFormat = toStringFormat;
            /*this.signedMessageParts = signedMessageParts;*/
            Anonymous = anonymous;
            DictionaryAnonymous = dictionaryAnonymous;

            if (anonymous != null)
            {
                AnonymousUri = new Uri(anonymous);
            }

            if (none != null)
            {
                NoneUri = new Uri(none);
            }

            FaultAction = faultAction;
            DefaultFaultAction = defaultFaultAction;
        }

        public static AddressingVersion WSAddressing10 { get; } = new AddressingVersion(Addressing10Strings.Namespace,
            XD.Addressing10Dictionary.Namespace, SR.Addressing10ToStringFormat, /*Addressing10SignedMessageParts,*/
            Addressing10Strings.Anonymous, XD.Addressing10Dictionary.Anonymous, Addressing10Strings.NoneAddress,
            Addressing10Strings.FaultAction, Addressing10Strings.DefaultFaultAction);

        public static AddressingVersion None { get; } = new AddressingVersion(AddressingNoneStrings.Namespace, XD.AddressingNoneDictionary.Namespace,
            SR.AddressingNoneToStringFormat, /*new MessagePartSpecification(),*/ null, null, null, null, null);

        public static AddressingVersion WSAddressingAugust2004 { get; } = new AddressingVersion(Addressing200408Strings.Namespace,
            XD.Addressing200408Dictionary.Namespace, SR.Addressing200408ToStringFormat, /*Addressing200408SignedMessageParts,*/
            Addressing200408Strings.Anonymous, XD.Addressing200408Dictionary.Anonymous, null,
            Addressing200408Strings.FaultAction, Addressing200408Strings.DefaultFaultAction);

        public string Namespace { get; }

        internal XmlDictionaryString DictionaryNamespace { get; }

        internal string Anonymous { get; }

        internal XmlDictionaryString DictionaryAnonymous { get; }

        public Uri AnonymousUri { get; }

        public Uri NoneUri { get; }

        public string FaultAction { get; } // the action for addressing faults

        internal string DefaultFaultAction { get; } // a default string that can be used for non-addressing faults

        public override string ToString()
        {
            return SR.Format(toStringFormat, Namespace);
        }
    }
}