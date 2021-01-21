// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF
{
    public sealed class EnvelopeVersion
    {
        private string _actor;
        private string _toStringFormat;

        private EnvelopeVersion(string ultimateReceiverActor, string nextDestinationActorValue,
            string ns, XmlDictionaryString dictionaryNs, string actor, XmlDictionaryString dictionaryActor,
            string toStringFormat, string senderFaultName, string receiverFaultName)
        {
            _toStringFormat = toStringFormat;
            UltimateDestinationActor = ultimateReceiverActor;
            NextDestinationActorValue = nextDestinationActorValue;
            Namespace = ns;
            DictionaryNamespace = dictionaryNs;
            _actor = actor;
            DictionaryActor = dictionaryActor;
            SenderFaultName = senderFaultName;
            ReceiverFaultName = receiverFaultName;

            if (ultimateReceiverActor != null)
            {
                if (ultimateReceiverActor.Length == 0)
                {
                    MustUnderstandActorValues = new string[] { "", nextDestinationActorValue };
                    UltimateDestinationActorValues = new string[] { "", nextDestinationActorValue };
                }
                else
                {
                    MustUnderstandActorValues = new string[] { "", ultimateReceiverActor, nextDestinationActorValue };
                    UltimateDestinationActorValues = new string[] { "", ultimateReceiverActor, nextDestinationActorValue };
                }
            }
        }

        internal XmlDictionaryString DictionaryActor { get; }

        internal string Namespace { get; }

        internal XmlDictionaryString DictionaryNamespace { get; }

        public string NextDestinationActorValue { get; }

        public static EnvelopeVersion None { get; } = new EnvelopeVersion(
            null,
            null,
            MessageStrings.Namespace,
            XD.MessageDictionary.Namespace,
            null,
            null,
            SR.EnvelopeNoneToStringFormat,
            "Sender",
            "Receiver");

        public static EnvelopeVersion Soap11 { get; } = new EnvelopeVersion(
            "",
            "http://schemas.xmlsoap.org/soap/actor/next",
            Message11Strings.Namespace,
            XD.Message11Dictionary.Namespace,
            Message11Strings.Actor,
            XD.Message11Dictionary.Actor,
            SR.Soap11ToStringFormat,
            "Client",
            "Server");

        public static EnvelopeVersion Soap12 { get; } = new EnvelopeVersion(
            "http://www.w3.org/2003/05/soap-envelope/role/ultimateReceiver",
            "http://www.w3.org/2003/05/soap-envelope/role/next",
            Message12Strings.Namespace,
            XD.Message12Dictionary.Namespace,
            Message12Strings.Role,
            XD.Message12Dictionary.Role,
            SR.Soap12ToStringFormat,
            "Sender",
            "Receiver");

        internal string ReceiverFaultName { get; }

        internal string[] MustUnderstandActorValues { get; }

        internal string UltimateDestinationActor { get; }

        public string[] GetUltimateDestinationActorValues() => (string[])UltimateDestinationActorValues.Clone();

        internal string[] UltimateDestinationActorValues { get; }

        internal bool IsUltimateDestinationActor(string actor)
        {
            return actor.Length == 0 || actor == UltimateDestinationActor || actor == NextDestinationActorValue;
        }

        public override string ToString()
        {
            return SR.Format(_toStringFormat, Namespace);
        }

        internal string SenderFaultName { get; }
    }
}