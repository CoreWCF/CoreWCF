// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF
{
    internal static class XD
    {
        public static ServiceModelDictionary Dictionary { get { return ServiceModelDictionary.CurrentVersion; } }

        private static MessageDictionary s_messageDictionary;
        private static Message11Dictionary s_message11Dictionary;
        private static Message12Dictionary s_message12Dictionary;

        public static MessageDictionary MessageDictionary
        {
            get
            {
                if (s_messageDictionary == null)
                {
                    s_messageDictionary = new MessageDictionary(Dictionary);
                }

                return s_messageDictionary;
            }
        }


        public static Message11Dictionary Message11Dictionary
        {
            get
            {
                if (s_message11Dictionary == null)
                {
                    s_message11Dictionary = new Message11Dictionary(Dictionary);
                }

                return s_message11Dictionary;
            }
        }

        public static Message12Dictionary Message12Dictionary
        {
            get
            {
                if (s_message12Dictionary == null)
                {
                    s_message12Dictionary = new Message12Dictionary(Dictionary);
                }

                return s_message12Dictionary;
            }
        }

    }

    internal class MessageDictionary
    {
        public XmlDictionaryString MustUnderstand;
        public XmlDictionaryString Envelope;
        public XmlDictionaryString Header;
        public XmlDictionaryString Body;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString Fault;
        public XmlDictionaryString MustUnderstandFault;
        public XmlDictionaryString Namespace;

        public MessageDictionary(ServiceModelDictionary dictionary)
        {
            MustUnderstand = dictionary.CreateString(ServiceModelStringsVersion1.String0, 0);
            Envelope = dictionary.CreateString(ServiceModelStringsVersion1.String1, 1);
            Header = dictionary.CreateString(ServiceModelStringsVersion1.String4, 4);
            Body = dictionary.CreateString(ServiceModelStringsVersion1.String7, 7);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String66, 66);
            Fault = dictionary.CreateString(ServiceModelStringsVersion1.String67, 67);
            MustUnderstandFault = dictionary.CreateString(ServiceModelStringsVersion1.String68, 68);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String440, 440);
        }
    }

    internal class Message11Dictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Actor;
        public XmlDictionaryString FaultCode;
        public XmlDictionaryString FaultString;
        public XmlDictionaryString FaultActor;
        public XmlDictionaryString FaultDetail;
        public XmlDictionaryString FaultNamespace;

        public Message11Dictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String481, 481);
            Actor = dictionary.CreateString(ServiceModelStringsVersion1.String482, 482);
            FaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String483, 483);
            FaultString = dictionary.CreateString(ServiceModelStringsVersion1.String484, 484);
            FaultActor = dictionary.CreateString(ServiceModelStringsVersion1.String485, 485);
            FaultDetail = dictionary.CreateString(ServiceModelStringsVersion1.String486, 486);
            FaultNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String81, 81);
        }
    }

    internal class Message12Dictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Role;
        public XmlDictionaryString Relay;
        public XmlDictionaryString FaultCode;
        public XmlDictionaryString FaultReason;
        public XmlDictionaryString FaultText;
        public XmlDictionaryString FaultNode;
        public XmlDictionaryString FaultRole;
        public XmlDictionaryString FaultDetail;
        public XmlDictionaryString FaultValue;
        public XmlDictionaryString FaultSubcode;
        public XmlDictionaryString NotUnderstood;
        public XmlDictionaryString QName;

        public Message12Dictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String2, 2);
            Role = dictionary.CreateString(ServiceModelStringsVersion1.String69, 69);
            Relay = dictionary.CreateString(ServiceModelStringsVersion1.String70, 70);
            FaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String71, 71);
            FaultReason = dictionary.CreateString(ServiceModelStringsVersion1.String72, 72);
            FaultText = dictionary.CreateString(ServiceModelStringsVersion1.String73, 73);
            FaultNode = dictionary.CreateString(ServiceModelStringsVersion1.String74, 74);
            FaultRole = dictionary.CreateString(ServiceModelStringsVersion1.String75, 75);
            FaultDetail = dictionary.CreateString(ServiceModelStringsVersion1.String76, 76);
            FaultValue = dictionary.CreateString(ServiceModelStringsVersion1.String77, 77);
            FaultSubcode = dictionary.CreateString(ServiceModelStringsVersion1.String78, 78);
            NotUnderstood = dictionary.CreateString(ServiceModelStringsVersion1.String79, 79);
            QName = dictionary.CreateString(ServiceModelStringsVersion1.String80, 80);
        }
    }

    internal static class MessageStrings
    {
        // Main dictionary strings
        public const string MustUnderstand = ServiceModelStringsVersion1.String0;
        public const string Envelope = ServiceModelStringsVersion1.String1;
        public const string Header = ServiceModelStringsVersion1.String4;
        public const string Body = ServiceModelStringsVersion1.String7;
        public const string Prefix = ServiceModelStringsVersion1.String66;
        public const string Fault = ServiceModelStringsVersion1.String67;
        public const string MustUnderstandFault = ServiceModelStringsVersion1.String68;
        public const string Namespace = ServiceModelStringsVersion1.String440;
    }
}
