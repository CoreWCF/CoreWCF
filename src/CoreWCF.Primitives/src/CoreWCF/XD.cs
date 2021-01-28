// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF
{
    // Static Xml Dictionary
    internal static class XD
    {
        static public ServiceModelDictionary Dictionary { get { return ServiceModelDictionary.CurrentVersion; } }

        private static ActivityIdFlowDictionary s_activityIdFlowDictionary;
        private static AddressingDictionary s_addressingDictionary;
        private static Addressing10Dictionary s_addressing10Dictionary;
        private static Addressing200408Dictionary s_addressing200408Dictionary;
        private static AddressingNoneDictionary s_addressingNoneDictionary;
        private static AtomicTransactionExternalDictionary s_atomicTransactionExternalDictionary;
        private static AtomicTransactionExternal10Dictionary s_atomicTransactionExternal10Dictionary;
        private static CoordinationExternalDictionary s_coordinationExternalDictionary;
        private static CoordinationExternal10Dictionary s_coordinationExternal10Dictionary;
        private static DotNetAddressingDictionary s_dotNetAddressingDictionary;
        private static DotNetAtomicTransactionExternalDictionary s_dotNetAtomicTransactionExternalDictionary;
        private static DotNetOneWayDictionary s_dotNetOneWayDictionary;
        private static DotNetSecurityDictionary s_dotNetSecurityDictionary;
        private static ExclusiveC14NDictionary s_exclusiveC14NDictionary;
        private static MessageDictionary s_messageDictionary;
        private static Message11Dictionary s_message11Dictionary;
        private static Message12Dictionary s_message12Dictionary;
        private static OleTxTransactionExternalDictionary s_oleTxTransactionExternalDictionary;
        private static PeerWireStringsDictionary s_peerWireStringsDictionary;
        private static PolicyDictionary s_policyDictionary;
        private static SamlDictionary s_samlDictionary;
        private static SecureConversationApr2004Dictionary s_secureConversationApr2004Dictionary;
        private static SecureConversationFeb2005Dictionary s_secureConversationFeb2005Dictionary;
        private static SecurityAlgorithmDictionary s_securityAlgorithmDictionary;
        private static SecurityJan2004Dictionary s_securityJan2004Dictionary;
        private static SecurityXXX2005Dictionary s_securityXXX2005Dictionary;
        private static SerializationDictionary s_serializationDictionary;
        private static TrustApr2004Dictionary s_trustApr2004Dictionary;
        private static TrustFeb2005Dictionary s_trustFeb2005Dictionary;
        private static UtilityDictionary s_utilityDictionary;
        private static WsrmFeb2005Dictionary s_wsrmFeb2005Dictionary;
        private static XmlEncryptionDictionary s_xmlEncryptionDictionary;
        private static XmlSignatureDictionary s_xmlSignatureDictionary;

        static public ActivityIdFlowDictionary ActivityIdFlowDictionary
        {
            get
            {
                if (s_activityIdFlowDictionary == null)
                {
                    s_activityIdFlowDictionary = new ActivityIdFlowDictionary(Dictionary);
                }

                return s_activityIdFlowDictionary;
            }
        }

        static public AddressingDictionary AddressingDictionary
        {
            get
            {
                if (s_addressingDictionary == null)
                {
                    s_addressingDictionary = new AddressingDictionary(Dictionary);
                }

                return s_addressingDictionary;
            }
        }

        static public Addressing10Dictionary Addressing10Dictionary
        {
            get
            {
                if (s_addressing10Dictionary == null)
                {
                    s_addressing10Dictionary = new Addressing10Dictionary(Dictionary);
                }

                return s_addressing10Dictionary;
            }
        }

        static public Addressing200408Dictionary Addressing200408Dictionary
        {
            get
            {
                if (s_addressing200408Dictionary == null)
                {
                    s_addressing200408Dictionary = new Addressing200408Dictionary(Dictionary);
                }

                return s_addressing200408Dictionary;
            }
        }

        static public AddressingNoneDictionary AddressingNoneDictionary
        {
            get
            {
                if (s_addressingNoneDictionary == null)
                {
                    s_addressingNoneDictionary = new AddressingNoneDictionary(Dictionary);
                }

                return s_addressingNoneDictionary;
            }
        }

        static public AtomicTransactionExternalDictionary AtomicTransactionExternalDictionary
        {
            get
            {
                if (s_atomicTransactionExternalDictionary == null)
                {
                    s_atomicTransactionExternalDictionary = new AtomicTransactionExternalDictionary(Dictionary);
                }

                return s_atomicTransactionExternalDictionary;
            }
        }

        static public AtomicTransactionExternal10Dictionary AtomicTransactionExternal10Dictionary
        {
            get
            {
                if (s_atomicTransactionExternal10Dictionary == null)
                {
                    s_atomicTransactionExternal10Dictionary = new AtomicTransactionExternal10Dictionary(Dictionary);
                }

                return s_atomicTransactionExternal10Dictionary;
            }
        }

        static public CoordinationExternalDictionary CoordinationExternalDictionary
        {
            get
            {
                if (s_coordinationExternalDictionary == null)
                {
                    s_coordinationExternalDictionary = new CoordinationExternalDictionary(Dictionary);
                }

                return s_coordinationExternalDictionary;
            }
        }

        static public CoordinationExternal10Dictionary CoordinationExternal10Dictionary
        {
            get
            {
                if (s_coordinationExternal10Dictionary == null)
                {
                    s_coordinationExternal10Dictionary = new CoordinationExternal10Dictionary(Dictionary);
                }

                return s_coordinationExternal10Dictionary;
            }
        }

        static public DotNetAddressingDictionary DotNetAddressingDictionary
        {
            get
            {
                if (s_dotNetAddressingDictionary == null)
                {
                    s_dotNetAddressingDictionary = new DotNetAddressingDictionary(Dictionary);
                }

                return s_dotNetAddressingDictionary;
            }
        }

        static public DotNetAtomicTransactionExternalDictionary DotNetAtomicTransactionExternalDictionary
        {
            get
            {
                if (s_dotNetAtomicTransactionExternalDictionary == null)
                {
                    s_dotNetAtomicTransactionExternalDictionary = new DotNetAtomicTransactionExternalDictionary(Dictionary);
                }

                return s_dotNetAtomicTransactionExternalDictionary;
            }
        }

        static public DotNetOneWayDictionary DotNetOneWayDictionary
        {
            get
            {
                if (s_dotNetOneWayDictionary == null)
                {
                    s_dotNetOneWayDictionary = new DotNetOneWayDictionary(Dictionary);
                }

                return s_dotNetOneWayDictionary;
            }
        }

        static public DotNetSecurityDictionary DotNetSecurityDictionary
        {
            get
            {
                if (s_dotNetSecurityDictionary == null)
                {
                    s_dotNetSecurityDictionary = new DotNetSecurityDictionary(Dictionary);
                }

                return s_dotNetSecurityDictionary;
            }
        }

        static public ExclusiveC14NDictionary ExclusiveC14NDictionary
        {
            get
            {
                if (s_exclusiveC14NDictionary == null)
                {
                    s_exclusiveC14NDictionary = new ExclusiveC14NDictionary(Dictionary);
                }

                return s_exclusiveC14NDictionary;
            }
        }

        static public MessageDictionary MessageDictionary
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

        static public Message11Dictionary Message11Dictionary
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

        static public Message12Dictionary Message12Dictionary
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

        static public OleTxTransactionExternalDictionary OleTxTransactionExternalDictionary
        {
            get
            {
                if (s_oleTxTransactionExternalDictionary == null)
                {
                    s_oleTxTransactionExternalDictionary = new OleTxTransactionExternalDictionary(Dictionary);
                }

                return s_oleTxTransactionExternalDictionary;
            }
        }

        static public PeerWireStringsDictionary PeerWireStringsDictionary
        {
            get
            {
                if (s_peerWireStringsDictionary == null)
                {
                    s_peerWireStringsDictionary = new PeerWireStringsDictionary(Dictionary);
                }

                return s_peerWireStringsDictionary;
            }
        }

        static public PolicyDictionary PolicyDictionary
        {
            get
            {
                if (s_policyDictionary == null)
                {
                    s_policyDictionary = new PolicyDictionary(Dictionary);
                }

                return s_policyDictionary;
            }
        }

        static public SamlDictionary SamlDictionary
        {
            get
            {
                if (s_samlDictionary == null)
                {
                    s_samlDictionary = new SamlDictionary(Dictionary);
                }

                return s_samlDictionary;
            }
        }

        static public SecureConversationApr2004Dictionary SecureConversationApr2004Dictionary
        {
            get
            {
                if (s_secureConversationApr2004Dictionary == null)
                {
                    s_secureConversationApr2004Dictionary = new SecureConversationApr2004Dictionary(Dictionary);
                }

                return s_secureConversationApr2004Dictionary;
            }
        }

        static public SecureConversationFeb2005Dictionary SecureConversationFeb2005Dictionary
        {
            get
            {
                if (s_secureConversationFeb2005Dictionary == null)
                {
                    s_secureConversationFeb2005Dictionary = new SecureConversationFeb2005Dictionary(Dictionary);
                }

                return s_secureConversationFeb2005Dictionary;
            }
        }

        static public SecurityAlgorithmDictionary SecurityAlgorithmDictionary
        {
            get
            {
                if (s_securityAlgorithmDictionary == null)
                {
                    s_securityAlgorithmDictionary = new SecurityAlgorithmDictionary(Dictionary);
                }

                return s_securityAlgorithmDictionary;
            }
        }

        static public SecurityJan2004Dictionary SecurityJan2004Dictionary
        {
            get
            {
                if (s_securityJan2004Dictionary == null)
                {
                    s_securityJan2004Dictionary = new SecurityJan2004Dictionary(Dictionary);
                }

                return s_securityJan2004Dictionary;
            }
        }

        static public SecurityXXX2005Dictionary SecurityXXX2005Dictionary
        {
            get
            {
                if (s_securityXXX2005Dictionary == null)
                {
                    s_securityXXX2005Dictionary = new SecurityXXX2005Dictionary(Dictionary);
                }

                return s_securityXXX2005Dictionary;
            }
        }

        static public SerializationDictionary SerializationDictionary
        {
            get
            {
                if (s_serializationDictionary == null)
                {
                    s_serializationDictionary = new SerializationDictionary(Dictionary);
                }

                return s_serializationDictionary;
            }
        }

        static public TrustApr2004Dictionary TrustApr2004Dictionary
        {
            get
            {
                if (s_trustApr2004Dictionary == null)
                {
                    s_trustApr2004Dictionary = new TrustApr2004Dictionary(Dictionary);
                }

                return s_trustApr2004Dictionary;
            }
        }

        static public TrustFeb2005Dictionary TrustFeb2005Dictionary
        {
            get
            {
                if (s_trustFeb2005Dictionary == null)
                {
                    s_trustFeb2005Dictionary = new TrustFeb2005Dictionary(Dictionary);
                }

                return s_trustFeb2005Dictionary;
            }
        }

        static public UtilityDictionary UtilityDictionary
        {
            get
            {
                if (s_utilityDictionary == null)
                {
                    s_utilityDictionary = new UtilityDictionary(Dictionary);
                }

                return s_utilityDictionary;
            }
        }

        static public WsrmFeb2005Dictionary WsrmFeb2005Dictionary
        {
            get
            {
                if (s_wsrmFeb2005Dictionary == null)
                {
                    s_wsrmFeb2005Dictionary = new WsrmFeb2005Dictionary(Dictionary);
                }

                return s_wsrmFeb2005Dictionary;
            }
        }

        static public XmlEncryptionDictionary XmlEncryptionDictionary
        {
            get
            {
                if (s_xmlEncryptionDictionary == null)
                {
                    s_xmlEncryptionDictionary = new XmlEncryptionDictionary(Dictionary);
                }

                return s_xmlEncryptionDictionary;
            }
        }

        static public XmlSignatureDictionary XmlSignatureDictionary
        {
            get
            {
                if (s_xmlSignatureDictionary == null)
                {
                    s_xmlSignatureDictionary = new XmlSignatureDictionary(Dictionary);
                }

                return s_xmlSignatureDictionary;
            }
        }
    }

    internal class ActivityIdFlowDictionary
    {
        public XmlDictionaryString ActivityId;
        public XmlDictionaryString ActivityIdNamespace;

        public ActivityIdFlowDictionary(ServiceModelDictionary dictionary)
        {
            ActivityId = dictionary.CreateString(ServiceModelStringsVersion1.String425, 425);
            ActivityIdNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String426, 426);
        }
    }

    internal class AddressingDictionary
    {
        public XmlDictionaryString Action;
        public XmlDictionaryString To;
        public XmlDictionaryString RelatesTo;
        public XmlDictionaryString MessageId;
        public XmlDictionaryString Address;
        public XmlDictionaryString ReplyTo;
        public XmlDictionaryString Empty;
        public XmlDictionaryString From;
        public XmlDictionaryString FaultTo;
        public XmlDictionaryString EndpointReference;
        public XmlDictionaryString PortType;
        public XmlDictionaryString ServiceName;
        public XmlDictionaryString PortName;
        public XmlDictionaryString ReferenceProperties;
        public XmlDictionaryString RelationshipType;
        public XmlDictionaryString Reply;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString IdentityExtensionNamespace;
        public XmlDictionaryString Identity;
        public XmlDictionaryString Spn;
        public XmlDictionaryString Upn;
        public XmlDictionaryString Rsa;
        public XmlDictionaryString Dns;
        public XmlDictionaryString X509v3Certificate;
        public XmlDictionaryString ReferenceParameters;
        public XmlDictionaryString IsReferenceParameter;

        public AddressingDictionary(ServiceModelDictionary dictionary)
        {
            Action = dictionary.CreateString(ServiceModelStringsVersion1.String5, 5);
            To = dictionary.CreateString(ServiceModelStringsVersion1.String6, 6);
            RelatesTo = dictionary.CreateString(ServiceModelStringsVersion1.String9, 9);
            MessageId = dictionary.CreateString(ServiceModelStringsVersion1.String13, 13);
            Address = dictionary.CreateString(ServiceModelStringsVersion1.String21, 21);
            ReplyTo = dictionary.CreateString(ServiceModelStringsVersion1.String22, 22);
            Empty = dictionary.CreateString(ServiceModelStringsVersion1.String81, 81);
            From = dictionary.CreateString(ServiceModelStringsVersion1.String82, 82);
            FaultTo = dictionary.CreateString(ServiceModelStringsVersion1.String83, 83);
            EndpointReference = dictionary.CreateString(ServiceModelStringsVersion1.String84, 84);
            PortType = dictionary.CreateString(ServiceModelStringsVersion1.String85, 85);
            ServiceName = dictionary.CreateString(ServiceModelStringsVersion1.String86, 86);
            PortName = dictionary.CreateString(ServiceModelStringsVersion1.String87, 87);
            ReferenceProperties = dictionary.CreateString(ServiceModelStringsVersion1.String88, 88);
            RelationshipType = dictionary.CreateString(ServiceModelStringsVersion1.String89, 89);
            Reply = dictionary.CreateString(ServiceModelStringsVersion1.String90, 90);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String91, 91);
            IdentityExtensionNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String92, 92);
            Identity = dictionary.CreateString(ServiceModelStringsVersion1.String93, 93);
            Spn = dictionary.CreateString(ServiceModelStringsVersion1.String94, 94);
            Upn = dictionary.CreateString(ServiceModelStringsVersion1.String95, 95);
            Rsa = dictionary.CreateString(ServiceModelStringsVersion1.String96, 96);
            Dns = dictionary.CreateString(ServiceModelStringsVersion1.String97, 97);
            X509v3Certificate = dictionary.CreateString(ServiceModelStringsVersion1.String98, 98);
            ReferenceParameters = dictionary.CreateString(ServiceModelStringsVersion1.String100, 100);
            IsReferenceParameter = dictionary.CreateString(ServiceModelStringsVersion1.String101, 101);
        }
    }

    internal class Addressing10Dictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Anonymous;
        public XmlDictionaryString FaultAction;
        public XmlDictionaryString ReplyRelationship;
        public XmlDictionaryString NoneAddress;
        public XmlDictionaryString Metadata;

        public Addressing10Dictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String3, 3);
            Anonymous = dictionary.CreateString(ServiceModelStringsVersion1.String10, 10);
            FaultAction = dictionary.CreateString(ServiceModelStringsVersion1.String99, 99);
            ReplyRelationship = dictionary.CreateString(ServiceModelStringsVersion1.String102, 102);
            NoneAddress = dictionary.CreateString(ServiceModelStringsVersion1.String103, 103);
            Metadata = dictionary.CreateString(ServiceModelStringsVersion1.String104, 104);
        }
    }

    internal class Addressing200408Dictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Anonymous;
        public XmlDictionaryString FaultAction;

        public Addressing200408Dictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String105, 105);
            Anonymous = dictionary.CreateString(ServiceModelStringsVersion1.String106, 106);
            FaultAction = dictionary.CreateString(ServiceModelStringsVersion1.String107, 107);
        }
    }

    internal class AddressingNoneDictionary
    {
        public XmlDictionaryString Namespace;

        public AddressingNoneDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String439, 439);
        }
    }

    internal class AtomicTransactionExternalDictionary
    {
        public XmlDictionaryString Prefix;
        public XmlDictionaryString Prepare;
        public XmlDictionaryString Prepared;
        public XmlDictionaryString ReadOnly;
        public XmlDictionaryString Commit;
        public XmlDictionaryString Rollback;
        public XmlDictionaryString Committed;
        public XmlDictionaryString Aborted;
        public XmlDictionaryString Replay;
        public XmlDictionaryString CompletionCoordinatorPortType;
        public XmlDictionaryString CompletionParticipantPortType;
        public XmlDictionaryString CoordinatorPortType;
        public XmlDictionaryString ParticipantPortType;
        public XmlDictionaryString InconsistentInternalState;

        public AtomicTransactionExternalDictionary(ServiceModelDictionary dictionary)
        {
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String383, 383);
            Prepare = dictionary.CreateString(ServiceModelStringsVersion1.String387, 387);
            Prepared = dictionary.CreateString(ServiceModelStringsVersion1.String388, 388);
            ReadOnly = dictionary.CreateString(ServiceModelStringsVersion1.String389, 389);
            Commit = dictionary.CreateString(ServiceModelStringsVersion1.String390, 390);
            Rollback = dictionary.CreateString(ServiceModelStringsVersion1.String391, 391);
            Committed = dictionary.CreateString(ServiceModelStringsVersion1.String392, 392);
            Aborted = dictionary.CreateString(ServiceModelStringsVersion1.String393, 393);
            Replay = dictionary.CreateString(ServiceModelStringsVersion1.String394, 394);
            CompletionCoordinatorPortType = dictionary.CreateString(ServiceModelStringsVersion1.String404, 404);
            CompletionParticipantPortType = dictionary.CreateString(ServiceModelStringsVersion1.String405, 405);
            CoordinatorPortType = dictionary.CreateString(ServiceModelStringsVersion1.String406, 406);
            ParticipantPortType = dictionary.CreateString(ServiceModelStringsVersion1.String407, 407);
            InconsistentInternalState = dictionary.CreateString(ServiceModelStringsVersion1.String408, 408);
        }
    }

    internal class AtomicTransactionExternal10Dictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString CompletionUri;
        public XmlDictionaryString Durable2PCUri;
        public XmlDictionaryString Volatile2PCUri;
        public XmlDictionaryString CommitAction;
        public XmlDictionaryString RollbackAction;
        public XmlDictionaryString CommittedAction;
        public XmlDictionaryString AbortedAction;
        public XmlDictionaryString PrepareAction;
        public XmlDictionaryString PreparedAction;
        public XmlDictionaryString ReadOnlyAction;
        public XmlDictionaryString ReplayAction;
        public XmlDictionaryString FaultAction;

        public AtomicTransactionExternal10Dictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String382, 382);
            CompletionUri = dictionary.CreateString(ServiceModelStringsVersion1.String384, 384);
            Durable2PCUri = dictionary.CreateString(ServiceModelStringsVersion1.String385, 385);
            Volatile2PCUri = dictionary.CreateString(ServiceModelStringsVersion1.String386, 386);
            CommitAction = dictionary.CreateString(ServiceModelStringsVersion1.String395, 395);
            RollbackAction = dictionary.CreateString(ServiceModelStringsVersion1.String396, 396);
            CommittedAction = dictionary.CreateString(ServiceModelStringsVersion1.String397, 397);
            AbortedAction = dictionary.CreateString(ServiceModelStringsVersion1.String398, 398);
            PrepareAction = dictionary.CreateString(ServiceModelStringsVersion1.String399, 399);
            PreparedAction = dictionary.CreateString(ServiceModelStringsVersion1.String400, 400);
            ReadOnlyAction = dictionary.CreateString(ServiceModelStringsVersion1.String401, 401);
            ReplayAction = dictionary.CreateString(ServiceModelStringsVersion1.String402, 402);
            FaultAction = dictionary.CreateString(ServiceModelStringsVersion1.String403, 403);
        }
    }

    internal class CoordinationExternalDictionary
    {
        public XmlDictionaryString Prefix;
        public XmlDictionaryString CreateCoordinationContext;
        public XmlDictionaryString CreateCoordinationContextResponse;
        public XmlDictionaryString CoordinationContext;
        public XmlDictionaryString CurrentContext;
        public XmlDictionaryString CoordinationType;
        public XmlDictionaryString RegistrationService;
        public XmlDictionaryString Register;
        public XmlDictionaryString RegisterResponse;
        public XmlDictionaryString Protocol;
        public XmlDictionaryString CoordinatorProtocolService;
        public XmlDictionaryString ParticipantProtocolService;
        public XmlDictionaryString Expires;
        public XmlDictionaryString Identifier;
        public XmlDictionaryString ActivationCoordinatorPortType;
        public XmlDictionaryString RegistrationCoordinatorPortType;
        public XmlDictionaryString InvalidState;
        public XmlDictionaryString InvalidProtocol;
        public XmlDictionaryString InvalidParameters;
        public XmlDictionaryString NoActivity;
        public XmlDictionaryString ContextRefused;
        public XmlDictionaryString AlreadyRegistered;

        public CoordinationExternalDictionary(ServiceModelDictionary dictionary)
        {
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String357, 357);
            CreateCoordinationContext = dictionary.CreateString(ServiceModelStringsVersion1.String358, 358);
            CreateCoordinationContextResponse = dictionary.CreateString(ServiceModelStringsVersion1.String359, 359);
            CoordinationContext = dictionary.CreateString(ServiceModelStringsVersion1.String360, 360);
            CurrentContext = dictionary.CreateString(ServiceModelStringsVersion1.String361, 361);
            CoordinationType = dictionary.CreateString(ServiceModelStringsVersion1.String362, 362);
            RegistrationService = dictionary.CreateString(ServiceModelStringsVersion1.String363, 363);
            Register = dictionary.CreateString(ServiceModelStringsVersion1.String364, 364);
            RegisterResponse = dictionary.CreateString(ServiceModelStringsVersion1.String365, 365);
            Protocol = dictionary.CreateString(ServiceModelStringsVersion1.String366, 366);
            CoordinatorProtocolService = dictionary.CreateString(ServiceModelStringsVersion1.String367, 367);
            ParticipantProtocolService = dictionary.CreateString(ServiceModelStringsVersion1.String368, 368);
            Expires = dictionary.CreateString(ServiceModelStringsVersion1.String55, 55);
            Identifier = dictionary.CreateString(ServiceModelStringsVersion1.String15, 15);
            ActivationCoordinatorPortType = dictionary.CreateString(ServiceModelStringsVersion1.String374, 374);
            RegistrationCoordinatorPortType = dictionary.CreateString(ServiceModelStringsVersion1.String375, 375);
            InvalidState = dictionary.CreateString(ServiceModelStringsVersion1.String376, 376);
            InvalidProtocol = dictionary.CreateString(ServiceModelStringsVersion1.String377, 377);
            InvalidParameters = dictionary.CreateString(ServiceModelStringsVersion1.String378, 378);
            NoActivity = dictionary.CreateString(ServiceModelStringsVersion1.String379, 379);
            ContextRefused = dictionary.CreateString(ServiceModelStringsVersion1.String380, 380);
            AlreadyRegistered = dictionary.CreateString(ServiceModelStringsVersion1.String381, 381);
        }
    }

    internal class CoordinationExternal10Dictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString CreateCoordinationContextAction;
        public XmlDictionaryString CreateCoordinationContextResponseAction;
        public XmlDictionaryString RegisterAction;
        public XmlDictionaryString RegisterResponseAction;
        public XmlDictionaryString FaultAction;

        public CoordinationExternal10Dictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String356, 356);
            CreateCoordinationContextAction = dictionary.CreateString(ServiceModelStringsVersion1.String369, 369);
            CreateCoordinationContextResponseAction = dictionary.CreateString(ServiceModelStringsVersion1.String370, 370);
            RegisterAction = dictionary.CreateString(ServiceModelStringsVersion1.String371, 371);
            RegisterResponseAction = dictionary.CreateString(ServiceModelStringsVersion1.String372, 372);
            FaultAction = dictionary.CreateString(ServiceModelStringsVersion1.String373, 373);
        }
    }

    internal class DotNetAddressingDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString RedirectTo;
        public XmlDictionaryString Via;

        public DotNetAddressingDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String108, 108);
            RedirectTo = dictionary.CreateString(ServiceModelStringsVersion1.String109, 109);
            Via = dictionary.CreateString(ServiceModelStringsVersion1.String110, 110);
        }
    }

    internal class DotNetAtomicTransactionExternalDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString Enlistment;
        public XmlDictionaryString Protocol;
        public XmlDictionaryString LocalTransactionId;
        public XmlDictionaryString IsolationLevel;
        public XmlDictionaryString IsolationFlags;
        public XmlDictionaryString Description;
        public XmlDictionaryString Loopback;
        public XmlDictionaryString RegisterInfo;
        public XmlDictionaryString ContextId;
        public XmlDictionaryString TokenId;
        public XmlDictionaryString AccessDenied;
        public XmlDictionaryString InvalidPolicy;
        public XmlDictionaryString CoordinatorRegistrationFailed;
        public XmlDictionaryString TooManyEnlistments;
        public XmlDictionaryString Disabled;

        public DotNetAtomicTransactionExternalDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String65, 65);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String409, 409);
            Enlistment = dictionary.CreateString(ServiceModelStringsVersion1.String410, 410);
            Protocol = dictionary.CreateString(ServiceModelStringsVersion1.String411, 411);
            LocalTransactionId = dictionary.CreateString(ServiceModelStringsVersion1.String412, 412);
            IsolationLevel = dictionary.CreateString(ServiceModelStringsVersion1.String413, 413);
            IsolationFlags = dictionary.CreateString(ServiceModelStringsVersion1.String414, 414);
            Description = dictionary.CreateString(ServiceModelStringsVersion1.String415, 415);
            Loopback = dictionary.CreateString(ServiceModelStringsVersion1.String416, 416);
            RegisterInfo = dictionary.CreateString(ServiceModelStringsVersion1.String417, 417);
            ContextId = dictionary.CreateString(ServiceModelStringsVersion1.String418, 418);
            TokenId = dictionary.CreateString(ServiceModelStringsVersion1.String419, 419);
            AccessDenied = dictionary.CreateString(ServiceModelStringsVersion1.String420, 420);
            InvalidPolicy = dictionary.CreateString(ServiceModelStringsVersion1.String421, 421);
            CoordinatorRegistrationFailed = dictionary.CreateString(ServiceModelStringsVersion1.String422, 422);
            TooManyEnlistments = dictionary.CreateString(ServiceModelStringsVersion1.String423, 423);
            Disabled = dictionary.CreateString(ServiceModelStringsVersion1.String424, 424);
        }
    }

    internal class DotNetOneWayDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString HeaderName;

        public DotNetOneWayDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String437, 437);
            HeaderName = dictionary.CreateString(ServiceModelStringsVersion1.String438, 438);
        }
    }

    internal class DotNetSecurityDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Prefix;

        public DotNetSecurityDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String162, 162);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String163, 163);
        }
    }

    internal class ExclusiveC14NDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString PrefixList;
        public XmlDictionaryString InclusiveNamespaces;
        public XmlDictionaryString Prefix;

        public ExclusiveC14NDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String111, 111);
            PrefixList = dictionary.CreateString(ServiceModelStringsVersion1.String112, 112);
            InclusiveNamespaces = dictionary.CreateString(ServiceModelStringsVersion1.String113, 113);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String114, 114);
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

    internal class OleTxTransactionExternalDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString OleTxTransaction;
        public XmlDictionaryString PropagationToken;

        public OleTxTransactionExternalDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String352, 352);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String353, 353);
            OleTxTransaction = dictionary.CreateString(ServiceModelStringsVersion1.String354, 354);
            PropagationToken = dictionary.CreateString(ServiceModelStringsVersion1.String355, 355);
        }
    }

    internal class PeerWireStringsDictionary
    {
        public XmlDictionaryString FloodAction;
        public XmlDictionaryString LinkUtilityAction;
        public XmlDictionaryString HopCount;
        public XmlDictionaryString HopCountNamespace;
        public XmlDictionaryString PeerVia;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Demuxer;
        public XmlDictionaryString PeerTo;

        public PeerWireStringsDictionary(ServiceModelDictionary dictionary)
        {
            FloodAction = dictionary.CreateString(ServiceModelStringsVersion1.String429, 429);
            LinkUtilityAction = dictionary.CreateString(ServiceModelStringsVersion1.String430, 430);
            HopCount = dictionary.CreateString(ServiceModelStringsVersion1.String431, 431);
            HopCountNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String432, 432);
            PeerVia = dictionary.CreateString(ServiceModelStringsVersion1.String433, 433);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String434, 434);
            Demuxer = dictionary.CreateString(ServiceModelStringsVersion1.String435, 435);
            PeerTo = dictionary.CreateString(ServiceModelStringsVersion1.String436, 436);
        }
    }

    internal class PolicyDictionary
    {
        public XmlDictionaryString Namespace;

        public PolicyDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String428, 428);
        }
    }

    internal class SamlDictionary
    {
        public XmlDictionaryString Access;
        public XmlDictionaryString AccessDecision;
        public XmlDictionaryString Action;
        public XmlDictionaryString Advice;
        public XmlDictionaryString Assertion;
        public XmlDictionaryString AssertionId;
        public XmlDictionaryString AssertionIdReference;
        public XmlDictionaryString Attribute;
        public XmlDictionaryString AttributeName;
        public XmlDictionaryString AttributeNamespace;
        public XmlDictionaryString AttributeStatement;
        public XmlDictionaryString AttributeValue;
        public XmlDictionaryString Audience;
        public XmlDictionaryString AudienceRestrictionCondition;
        public XmlDictionaryString AuthenticationInstant;
        public XmlDictionaryString AuthenticationMethod;
        public XmlDictionaryString AuthenticationStatement;
        public XmlDictionaryString AuthorityBinding;
        public XmlDictionaryString AuthorityKind;
        public XmlDictionaryString AuthorizationDecisionStatement;
        public XmlDictionaryString Binding;
        public XmlDictionaryString Condition;
        public XmlDictionaryString Conditions;
        public XmlDictionaryString Decision;
        public XmlDictionaryString DoNotCacheCondition;
        public XmlDictionaryString Evidence;
        public XmlDictionaryString IssueInstant;
        public XmlDictionaryString Issuer;
        public XmlDictionaryString Location;
        public XmlDictionaryString MajorVersion;
        public XmlDictionaryString MinorVersion;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString NameIdentifier;
        public XmlDictionaryString NameIdentifierFormat;
        public XmlDictionaryString NameIdentifierNameQualifier;
        public XmlDictionaryString ActionNamespaceAttribute;
        public XmlDictionaryString NotBefore;
        public XmlDictionaryString NotOnOrAfter;
        public XmlDictionaryString PreferredPrefix;
        public XmlDictionaryString Statement;
        public XmlDictionaryString Subject;
        public XmlDictionaryString SubjectConfirmation;
        public XmlDictionaryString SubjectConfirmationData;
        public XmlDictionaryString SubjectConfirmationMethod;
        public XmlDictionaryString HolderOfKey;
        public XmlDictionaryString SenderVouches;
        public XmlDictionaryString SubjectLocality;
        public XmlDictionaryString SubjectLocalityDNSAddress;
        public XmlDictionaryString SubjectLocalityIPAddress;
        public XmlDictionaryString SubjectStatement;
        public XmlDictionaryString UnspecifiedAuthenticationMethod;
        public XmlDictionaryString NamespaceAttributePrefix;
        public XmlDictionaryString Resource;
        public XmlDictionaryString UserName;
        public XmlDictionaryString UserNameNamespace;
        public XmlDictionaryString EmailName;
        public XmlDictionaryString EmailNamespace;

        public SamlDictionary(ServiceModelDictionary dictionary)
        {
            Access = dictionary.CreateString(ServiceModelStringsVersion1.String251, 251);
            AccessDecision = dictionary.CreateString(ServiceModelStringsVersion1.String252, 252);
            Action = dictionary.CreateString(ServiceModelStringsVersion1.String5, 5);
            Advice = dictionary.CreateString(ServiceModelStringsVersion1.String253, 253);
            Assertion = dictionary.CreateString(ServiceModelStringsVersion1.String179, 179);
            AssertionId = dictionary.CreateString(ServiceModelStringsVersion1.String254, 254);
            AssertionIdReference = dictionary.CreateString(ServiceModelStringsVersion1.String255, 255);
            Attribute = dictionary.CreateString(ServiceModelStringsVersion1.String256, 256);
            AttributeName = dictionary.CreateString(ServiceModelStringsVersion1.String257, 257);
            AttributeNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String258, 258);
            AttributeStatement = dictionary.CreateString(ServiceModelStringsVersion1.String259, 259);
            AttributeValue = dictionary.CreateString(ServiceModelStringsVersion1.String260, 260);
            Audience = dictionary.CreateString(ServiceModelStringsVersion1.String261, 261);
            AudienceRestrictionCondition = dictionary.CreateString(ServiceModelStringsVersion1.String262, 262);
            AuthenticationInstant = dictionary.CreateString(ServiceModelStringsVersion1.String263, 263);
            AuthenticationMethod = dictionary.CreateString(ServiceModelStringsVersion1.String264, 264);
            AuthenticationStatement = dictionary.CreateString(ServiceModelStringsVersion1.String265, 265);
            AuthorityBinding = dictionary.CreateString(ServiceModelStringsVersion1.String266, 266);
            AuthorityKind = dictionary.CreateString(ServiceModelStringsVersion1.String267, 267);
            AuthorizationDecisionStatement = dictionary.CreateString(ServiceModelStringsVersion1.String268, 268);
            Binding = dictionary.CreateString(ServiceModelStringsVersion1.String269, 269);
            Condition = dictionary.CreateString(ServiceModelStringsVersion1.String270, 270);
            Conditions = dictionary.CreateString(ServiceModelStringsVersion1.String271, 271);
            Decision = dictionary.CreateString(ServiceModelStringsVersion1.String272, 272);
            DoNotCacheCondition = dictionary.CreateString(ServiceModelStringsVersion1.String273, 273);
            Evidence = dictionary.CreateString(ServiceModelStringsVersion1.String274, 274);
            IssueInstant = dictionary.CreateString(ServiceModelStringsVersion1.String275, 275);
            Issuer = dictionary.CreateString(ServiceModelStringsVersion1.String276, 276);
            Location = dictionary.CreateString(ServiceModelStringsVersion1.String277, 277);
            MajorVersion = dictionary.CreateString(ServiceModelStringsVersion1.String278, 278);
            MinorVersion = dictionary.CreateString(ServiceModelStringsVersion1.String279, 279);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String180, 180);
            NameIdentifier = dictionary.CreateString(ServiceModelStringsVersion1.String280, 280);
            NameIdentifierFormat = dictionary.CreateString(ServiceModelStringsVersion1.String281, 281);
            NameIdentifierNameQualifier = dictionary.CreateString(ServiceModelStringsVersion1.String282, 282);
            ActionNamespaceAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String283, 283);
            NotBefore = dictionary.CreateString(ServiceModelStringsVersion1.String284, 284);
            NotOnOrAfter = dictionary.CreateString(ServiceModelStringsVersion1.String285, 285);
            PreferredPrefix = dictionary.CreateString(ServiceModelStringsVersion1.String286, 286);
            Statement = dictionary.CreateString(ServiceModelStringsVersion1.String287, 287);
            Subject = dictionary.CreateString(ServiceModelStringsVersion1.String288, 288);
            SubjectConfirmation = dictionary.CreateString(ServiceModelStringsVersion1.String289, 289);
            SubjectConfirmationData = dictionary.CreateString(ServiceModelStringsVersion1.String290, 290);
            SubjectConfirmationMethod = dictionary.CreateString(ServiceModelStringsVersion1.String291, 291);
            HolderOfKey = dictionary.CreateString(ServiceModelStringsVersion1.String292, 292);
            SenderVouches = dictionary.CreateString(ServiceModelStringsVersion1.String293, 293);
            SubjectLocality = dictionary.CreateString(ServiceModelStringsVersion1.String294, 294);
            SubjectLocalityDNSAddress = dictionary.CreateString(ServiceModelStringsVersion1.String295, 295);
            SubjectLocalityIPAddress = dictionary.CreateString(ServiceModelStringsVersion1.String296, 296);
            SubjectStatement = dictionary.CreateString(ServiceModelStringsVersion1.String297, 297);
            UnspecifiedAuthenticationMethod = dictionary.CreateString(ServiceModelStringsVersion1.String298, 298);
            NamespaceAttributePrefix = dictionary.CreateString(ServiceModelStringsVersion1.String299, 299);
            Resource = dictionary.CreateString(ServiceModelStringsVersion1.String300, 300);
            UserName = dictionary.CreateString(ServiceModelStringsVersion1.String301, 301);
            UserNameNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String302, 302);
            EmailName = dictionary.CreateString(ServiceModelStringsVersion1.String303, 303);
            EmailNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String304, 304);
        }
    }

    public class SecureConversationDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString DerivedKeyToken;
        public XmlDictionaryString Nonce;
        public XmlDictionaryString Length;
        public XmlDictionaryString SecurityContextToken;
        public XmlDictionaryString AlgorithmAttribute;
        public XmlDictionaryString Generation;
        public XmlDictionaryString Label;
        public XmlDictionaryString Offset;
        public XmlDictionaryString Properties;
        public XmlDictionaryString Identifier;
        public XmlDictionaryString Cookie;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString DerivedKeyTokenType;
        public XmlDictionaryString SecurityContextTokenType;
        public XmlDictionaryString SecurityContextTokenReferenceValueType;
        public XmlDictionaryString RequestSecurityContextIssuance;
        public XmlDictionaryString RequestSecurityContextIssuanceResponse;
        public XmlDictionaryString RenewNeededFaultCode;
        public XmlDictionaryString BadContextTokenFaultCode;

        public SecureConversationDictionary()
        {
        }
        internal SecureConversationDictionary(ServiceModelDictionary dictionary)
        {
        }
    }

    internal class SecureConversationApr2004Dictionary : SecureConversationDictionary
    {
        public SecureConversationApr2004Dictionary(ServiceModelDictionary dictionary)
            : base(dictionary)
        {
            SecurityContextToken = dictionary.CreateString(ServiceModelStringsVersion1.String115, 115);
            DerivedKeyToken = dictionary.CreateString(ServiceModelStringsVersion1.String39, 39);
            AlgorithmAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String8, 8);
            Generation = dictionary.CreateString(ServiceModelStringsVersion1.String116, 116);
            Label = dictionary.CreateString(ServiceModelStringsVersion1.String117, 117);
            Length = dictionary.CreateString(ServiceModelStringsVersion1.String56, 56);
            Nonce = dictionary.CreateString(ServiceModelStringsVersion1.String40, 40);
            Offset = dictionary.CreateString(ServiceModelStringsVersion1.String118, 118);
            Properties = dictionary.CreateString(ServiceModelStringsVersion1.String119, 119);
            Identifier = dictionary.CreateString(ServiceModelStringsVersion1.String15, 15);
            Cookie = dictionary.CreateString(ServiceModelStringsVersion1.String120, 120);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String121, 121);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String122, 122);
            DerivedKeyTokenType = dictionary.CreateString(ServiceModelStringsVersion1.String123, 123);
            SecurityContextTokenType = dictionary.CreateString(ServiceModelStringsVersion1.String124, 124);
            SecurityContextTokenReferenceValueType = dictionary.CreateString(ServiceModelStringsVersion1.String124, 124);
            RequestSecurityContextIssuance = dictionary.CreateString(ServiceModelStringsVersion1.String125, 125);
            RequestSecurityContextIssuanceResponse = dictionary.CreateString(ServiceModelStringsVersion1.String126, 126);
            RenewNeededFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String127, 127);
            BadContextTokenFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String128, 128);
        }
    }

    internal class SecureConversationFeb2005Dictionary : SecureConversationDictionary
    {
        public XmlDictionaryString RequestSecurityContextRenew;
        public XmlDictionaryString RequestSecurityContextRenewResponse;
        public XmlDictionaryString RequestSecurityContextClose;
        public XmlDictionaryString RequestSecurityContextCloseResponse;

        public SecureConversationFeb2005Dictionary(ServiceModelDictionary dictionary)
            : base(dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String38, 38);
            DerivedKeyToken = dictionary.CreateString(ServiceModelStringsVersion1.String39, 39);
            Nonce = dictionary.CreateString(ServiceModelStringsVersion1.String40, 40);
            Length = dictionary.CreateString(ServiceModelStringsVersion1.String56, 56);
            SecurityContextToken = dictionary.CreateString(ServiceModelStringsVersion1.String115, 115);
            AlgorithmAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String8, 8);
            Generation = dictionary.CreateString(ServiceModelStringsVersion1.String116, 116);
            Label = dictionary.CreateString(ServiceModelStringsVersion1.String117, 117);
            Offset = dictionary.CreateString(ServiceModelStringsVersion1.String118, 118);
            Properties = dictionary.CreateString(ServiceModelStringsVersion1.String119, 119);
            Identifier = dictionary.CreateString(ServiceModelStringsVersion1.String15, 15);
            Cookie = dictionary.CreateString(ServiceModelStringsVersion1.String120, 120);
            RenewNeededFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String127, 127);
            BadContextTokenFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String128, 128);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String129, 129);
            DerivedKeyTokenType = dictionary.CreateString(ServiceModelStringsVersion1.String130, 130);
            SecurityContextTokenType = dictionary.CreateString(ServiceModelStringsVersion1.String131, 131);
            SecurityContextTokenReferenceValueType = dictionary.CreateString(ServiceModelStringsVersion1.String131, 131);
            RequestSecurityContextIssuance = dictionary.CreateString(ServiceModelStringsVersion1.String132, 132);
            RequestSecurityContextIssuanceResponse = dictionary.CreateString(ServiceModelStringsVersion1.String133, 133);
            RequestSecurityContextRenew = dictionary.CreateString(ServiceModelStringsVersion1.String134, 134);
            RequestSecurityContextRenewResponse = dictionary.CreateString(ServiceModelStringsVersion1.String135, 135);
            RequestSecurityContextClose = dictionary.CreateString(ServiceModelStringsVersion1.String136, 136);
            RequestSecurityContextCloseResponse = dictionary.CreateString(ServiceModelStringsVersion1.String137, 137);
        }
    }

    internal class SecurityAlgorithmDictionary
    {
        public XmlDictionaryString Aes128Encryption;
        public XmlDictionaryString Aes128KeyWrap;
        public XmlDictionaryString Aes192Encryption;
        public XmlDictionaryString Aes192KeyWrap;
        public XmlDictionaryString Aes256Encryption;
        public XmlDictionaryString Aes256KeyWrap;
        public XmlDictionaryString DesEncryption;
        public XmlDictionaryString DsaSha1Signature;
        public XmlDictionaryString ExclusiveC14n;
        public XmlDictionaryString ExclusiveC14nWithComments;
        public XmlDictionaryString HmacSha1Signature;
        public XmlDictionaryString HmacSha256Signature;
        public XmlDictionaryString Psha1KeyDerivation;
        public XmlDictionaryString Ripemd160Digest;
        public XmlDictionaryString RsaOaepKeyWrap;
        public XmlDictionaryString RsaSha1Signature;
        public XmlDictionaryString RsaSha256Signature;
        public XmlDictionaryString RsaV15KeyWrap;
        public XmlDictionaryString Sha1Digest;
        public XmlDictionaryString Sha256Digest;
        public XmlDictionaryString Sha512Digest;
        public XmlDictionaryString TripleDesEncryption;
        public XmlDictionaryString TripleDesKeyWrap;
        public XmlDictionaryString TlsSspiKeyWrap;
        public XmlDictionaryString WindowsSspiKeyWrap;

        public SecurityAlgorithmDictionary(ServiceModelDictionary dictionary)
        {
            Aes128Encryption = dictionary.CreateString(ServiceModelStringsVersion1.String138, 138);
            Aes128KeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String139, 139);
            Aes192Encryption = dictionary.CreateString(ServiceModelStringsVersion1.String140, 140);
            Aes192KeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String141, 141);
            Aes256Encryption = dictionary.CreateString(ServiceModelStringsVersion1.String142, 142);
            Aes256KeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String143, 143);
            DesEncryption = dictionary.CreateString(ServiceModelStringsVersion1.String144, 144);
            DsaSha1Signature = dictionary.CreateString(ServiceModelStringsVersion1.String145, 145);
            ExclusiveC14n = dictionary.CreateString(ServiceModelStringsVersion1.String111, 111);
            ExclusiveC14nWithComments = dictionary.CreateString(ServiceModelStringsVersion1.String146, 146);
            HmacSha1Signature = dictionary.CreateString(ServiceModelStringsVersion1.String147, 147);
            HmacSha256Signature = dictionary.CreateString(ServiceModelStringsVersion1.String148, 148);
            Psha1KeyDerivation = dictionary.CreateString(ServiceModelStringsVersion1.String149, 149);
            Ripemd160Digest = dictionary.CreateString(ServiceModelStringsVersion1.String150, 150);
            RsaOaepKeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String151, 151);
            RsaSha1Signature = dictionary.CreateString(ServiceModelStringsVersion1.String152, 152);
            RsaSha256Signature = dictionary.CreateString(ServiceModelStringsVersion1.String153, 153);
            RsaV15KeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String154, 154);
            Sha1Digest = dictionary.CreateString(ServiceModelStringsVersion1.String155, 155);
            Sha256Digest = dictionary.CreateString(ServiceModelStringsVersion1.String156, 156);
            Sha512Digest = dictionary.CreateString(ServiceModelStringsVersion1.String157, 157);
            TripleDesEncryption = dictionary.CreateString(ServiceModelStringsVersion1.String158, 158);
            TripleDesKeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String159, 159);
            TlsSspiKeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String160, 160);
            WindowsSspiKeyWrap = dictionary.CreateString(ServiceModelStringsVersion1.String161, 161);
        }
    }

    internal class SecurityJan2004Dictionary
    {
        public XmlDictionaryString SecurityTokenReference;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Security;
        public XmlDictionaryString ValueType;
        public XmlDictionaryString TypeAttribute;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString NonceElement;
        public XmlDictionaryString PasswordElement;
        public XmlDictionaryString PasswordTextName;
        public XmlDictionaryString UserNameElement;
        public XmlDictionaryString UserNameTokenElement;
        public XmlDictionaryString BinarySecurityToken;
        public XmlDictionaryString EncodingType;
        public XmlDictionaryString Reference;
        public XmlDictionaryString URI;
        public XmlDictionaryString KeyIdentifier;
        public XmlDictionaryString EncodingTypeValueBase64Binary;
        public XmlDictionaryString EncodingTypeValueHexBinary;
        public XmlDictionaryString EncodingTypeValueText;
        public XmlDictionaryString X509SKIValueType;
        public XmlDictionaryString KerberosTokenTypeGSS;
        public XmlDictionaryString KerberosTokenType1510;
        public XmlDictionaryString SamlAssertionIdValueType;
        public XmlDictionaryString SamlAssertion;
        public XmlDictionaryString SamlUri;
        public XmlDictionaryString RelAssertionValueType;
        public XmlDictionaryString FailedAuthenticationFaultCode;
        public XmlDictionaryString InvalidSecurityTokenFaultCode;
        public XmlDictionaryString InvalidSecurityFaultCode;
        public XmlDictionaryString KerberosHashValueType;

        public SecurityJan2004Dictionary(ServiceModelDictionary dictionary)
        {
            SecurityTokenReference = dictionary.CreateString(ServiceModelStringsVersion1.String30, 30);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String36, 36);
            Security = dictionary.CreateString(ServiceModelStringsVersion1.String52, 52);
            ValueType = dictionary.CreateString(ServiceModelStringsVersion1.String58, 58);
            TypeAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String59, 59);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String164, 164);
            NonceElement = dictionary.CreateString(ServiceModelStringsVersion1.String40, 40);
            PasswordElement = dictionary.CreateString(ServiceModelStringsVersion1.String165, 165);
            PasswordTextName = dictionary.CreateString(ServiceModelStringsVersion1.String166, 166);
            UserNameElement = dictionary.CreateString(ServiceModelStringsVersion1.String167, 167);
            UserNameTokenElement = dictionary.CreateString(ServiceModelStringsVersion1.String168, 168);
            BinarySecurityToken = dictionary.CreateString(ServiceModelStringsVersion1.String169, 169);
            EncodingType = dictionary.CreateString(ServiceModelStringsVersion1.String170, 170);
            Reference = dictionary.CreateString(ServiceModelStringsVersion1.String12, 12);
            URI = dictionary.CreateString(ServiceModelStringsVersion1.String11, 11);
            KeyIdentifier = dictionary.CreateString(ServiceModelStringsVersion1.String171, 171);
            EncodingTypeValueBase64Binary = dictionary.CreateString(ServiceModelStringsVersion1.String172, 172);
            EncodingTypeValueHexBinary = dictionary.CreateString(ServiceModelStringsVersion1.String173, 173);
            EncodingTypeValueText = dictionary.CreateString(ServiceModelStringsVersion1.String174, 174);
            X509SKIValueType = dictionary.CreateString(ServiceModelStringsVersion1.String175, 175);
            KerberosTokenTypeGSS = dictionary.CreateString(ServiceModelStringsVersion1.String176, 176);
            KerberosTokenType1510 = dictionary.CreateString(ServiceModelStringsVersion1.String177, 177);
            SamlAssertionIdValueType = dictionary.CreateString(ServiceModelStringsVersion1.String178, 178);
            SamlAssertion = dictionary.CreateString(ServiceModelStringsVersion1.String179, 179);
            SamlUri = dictionary.CreateString(ServiceModelStringsVersion1.String180, 180);
            RelAssertionValueType = dictionary.CreateString(ServiceModelStringsVersion1.String181, 181);
            FailedAuthenticationFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String182, 182);
            InvalidSecurityTokenFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String183, 183);
            InvalidSecurityFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String184, 184);
            KerberosHashValueType = dictionary.CreateString(ServiceModelStringsVersion1.String427, 427);
        }
    }

    internal class SecurityXXX2005Dictionary
    {
        public XmlDictionaryString EncryptedHeader;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString SignatureConfirmation;
        public XmlDictionaryString ValueAttribute;
        public XmlDictionaryString TokenTypeAttribute;
        public XmlDictionaryString ThumbprintSha1ValueType;
        public XmlDictionaryString EncryptedKeyTokenType;
        public XmlDictionaryString EncryptedKeyHashValueType;
        public XmlDictionaryString SamlTokenType;
        public XmlDictionaryString Saml20TokenType;
        public XmlDictionaryString Saml11AssertionValueType;

        public SecurityXXX2005Dictionary(ServiceModelDictionary dictionary)
        {
            EncryptedHeader = dictionary.CreateString(ServiceModelStringsVersion1.String60, 60);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String61, 61);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String185, 185);
            SignatureConfirmation = dictionary.CreateString(ServiceModelStringsVersion1.String186, 186);
            ValueAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String77, 77);
            TokenTypeAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String187, 187);
            ThumbprintSha1ValueType = dictionary.CreateString(ServiceModelStringsVersion1.String188, 188);
            EncryptedKeyTokenType = dictionary.CreateString(ServiceModelStringsVersion1.String189, 189);
            EncryptedKeyHashValueType = dictionary.CreateString(ServiceModelStringsVersion1.String190, 190);
            SamlTokenType = dictionary.CreateString(ServiceModelStringsVersion1.String191, 191);
            Saml20TokenType = dictionary.CreateString(ServiceModelStringsVersion1.String192, 192);
            Saml11AssertionValueType = dictionary.CreateString(ServiceModelStringsVersion1.String193, 193);
        }
    }

    internal class SerializationDictionary
    {
        public XmlDictionaryString XmlSchemaInstanceNamespace;
        public XmlDictionaryString XmlSchemaNamespace;
        public XmlDictionaryString Nil;
        public XmlDictionaryString Type;
        public XmlDictionaryString Char;
        public XmlDictionaryString Boolean;
        public XmlDictionaryString Byte;
        public XmlDictionaryString UnsignedByte;
        public XmlDictionaryString Short;
        public XmlDictionaryString UnsignedShort;
        public XmlDictionaryString Int;
        public XmlDictionaryString UnsignedInt;
        public XmlDictionaryString Long;
        public XmlDictionaryString UnsignedLong;
        public XmlDictionaryString Float;
        public XmlDictionaryString Double;
        public XmlDictionaryString Decimal;
        public XmlDictionaryString DateTime;
        public XmlDictionaryString String;
        public XmlDictionaryString Base64Binary;
        public XmlDictionaryString AnyType;
        public XmlDictionaryString Duration;
        public XmlDictionaryString Guid;
        public XmlDictionaryString AnyURI;
        public XmlDictionaryString QName;
        public XmlDictionaryString Time;
        public XmlDictionaryString Date;
        public XmlDictionaryString HexBinary;
        public XmlDictionaryString GYearMonth;
        public XmlDictionaryString GYear;
        public XmlDictionaryString GMonthDay;
        public XmlDictionaryString GDay;
        public XmlDictionaryString GMonth;
        public XmlDictionaryString Integer;
        public XmlDictionaryString PositiveInteger;
        public XmlDictionaryString NegativeInteger;
        public XmlDictionaryString NonPositiveInteger;
        public XmlDictionaryString NonNegativeInteger;
        public XmlDictionaryString NormalizedString;

        public SerializationDictionary(ServiceModelDictionary dictionary)
        {
            XmlSchemaInstanceNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String441, 441);
            XmlSchemaNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String442, 442);
            Nil = dictionary.CreateString(ServiceModelStringsVersion1.String443, 443);
            Type = dictionary.CreateString(ServiceModelStringsVersion1.String444, 444);
            Char = dictionary.CreateString(ServiceModelStringsVersion1.String445, 445);
            Boolean = dictionary.CreateString(ServiceModelStringsVersion1.String446, 446);
            Byte = dictionary.CreateString(ServiceModelStringsVersion1.String447, 447);
            UnsignedByte = dictionary.CreateString(ServiceModelStringsVersion1.String448, 448);
            Short = dictionary.CreateString(ServiceModelStringsVersion1.String449, 449);
            UnsignedShort = dictionary.CreateString(ServiceModelStringsVersion1.String450, 450);
            Int = dictionary.CreateString(ServiceModelStringsVersion1.String451, 451);
            UnsignedInt = dictionary.CreateString(ServiceModelStringsVersion1.String452, 452);
            Long = dictionary.CreateString(ServiceModelStringsVersion1.String453, 453);
            UnsignedLong = dictionary.CreateString(ServiceModelStringsVersion1.String454, 454);
            Float = dictionary.CreateString(ServiceModelStringsVersion1.String455, 455);
            Double = dictionary.CreateString(ServiceModelStringsVersion1.String456, 456);
            Decimal = dictionary.CreateString(ServiceModelStringsVersion1.String457, 457);
            DateTime = dictionary.CreateString(ServiceModelStringsVersion1.String458, 458);
            String = dictionary.CreateString(ServiceModelStringsVersion1.String459, 459);
            Base64Binary = dictionary.CreateString(ServiceModelStringsVersion1.String460, 460);
            AnyType = dictionary.CreateString(ServiceModelStringsVersion1.String461, 461);
            Duration = dictionary.CreateString(ServiceModelStringsVersion1.String462, 462);
            Guid = dictionary.CreateString(ServiceModelStringsVersion1.String463, 463);
            AnyURI = dictionary.CreateString(ServiceModelStringsVersion1.String464, 464);
            QName = dictionary.CreateString(ServiceModelStringsVersion1.String465, 465);
            Time = dictionary.CreateString(ServiceModelStringsVersion1.String466, 466);
            Date = dictionary.CreateString(ServiceModelStringsVersion1.String467, 467);
            HexBinary = dictionary.CreateString(ServiceModelStringsVersion1.String468, 468);
            GYearMonth = dictionary.CreateString(ServiceModelStringsVersion1.String469, 469);
            GYear = dictionary.CreateString(ServiceModelStringsVersion1.String470, 470);
            GMonthDay = dictionary.CreateString(ServiceModelStringsVersion1.String471, 471);
            GDay = dictionary.CreateString(ServiceModelStringsVersion1.String472, 472);
            GMonth = dictionary.CreateString(ServiceModelStringsVersion1.String473, 473);
            Integer = dictionary.CreateString(ServiceModelStringsVersion1.String474, 474);
            PositiveInteger = dictionary.CreateString(ServiceModelStringsVersion1.String475, 475);
            NegativeInteger = dictionary.CreateString(ServiceModelStringsVersion1.String476, 476);
            NonPositiveInteger = dictionary.CreateString(ServiceModelStringsVersion1.String477, 477);
            NonNegativeInteger = dictionary.CreateString(ServiceModelStringsVersion1.String478, 478);
            NormalizedString = dictionary.CreateString(ServiceModelStringsVersion1.String479, 479);
        }
    }

    internal class TrustDictionary
    {
        public XmlDictionaryString RequestSecurityTokenResponseCollection;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString BinarySecretClauseType;
        public XmlDictionaryString CombinedHashLabel;
        public XmlDictionaryString RequestSecurityTokenResponse;
        public XmlDictionaryString TokenType;
        public XmlDictionaryString KeySize;
        public XmlDictionaryString RequestedTokenReference;
        public XmlDictionaryString AppliesTo;
        public XmlDictionaryString Authenticator;
        public XmlDictionaryString CombinedHash;
        public XmlDictionaryString BinaryExchange;
        public XmlDictionaryString Lifetime;
        public XmlDictionaryString RequestedSecurityToken;
        public XmlDictionaryString Entropy;
        public XmlDictionaryString RequestedProofToken;
        public XmlDictionaryString ComputedKey;
        public XmlDictionaryString RequestSecurityToken;
        public XmlDictionaryString RequestType;
        public XmlDictionaryString Context;
        public XmlDictionaryString BinarySecret;
        public XmlDictionaryString Type;
        public XmlDictionaryString SpnegoValueTypeUri;
        public XmlDictionaryString TlsnegoValueTypeUri;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString RequestSecurityTokenIssuance;
        public XmlDictionaryString RequestSecurityTokenIssuanceResponse;
        public XmlDictionaryString RequestTypeIssue;
        public XmlDictionaryString Psha1ComputedKeyUri;
        public XmlDictionaryString SymmetricKeyBinarySecret;
        public XmlDictionaryString NonceBinarySecret;
        public XmlDictionaryString KeyType;
        public XmlDictionaryString SymmetricKeyType;
        public XmlDictionaryString PublicKeyType;
        public XmlDictionaryString Claims;
        public XmlDictionaryString InvalidRequestFaultCode;
        public XmlDictionaryString FailedAuthenticationFaultCode;
        public XmlDictionaryString RequestFailedFaultCode;
        public XmlDictionaryString SignWith;
        public XmlDictionaryString EncryptWith;
        public XmlDictionaryString EncryptionAlgorithm;
        public XmlDictionaryString CanonicalizationAlgorithm;
        public XmlDictionaryString ComputedKeyAlgorithm;
        public XmlDictionaryString UseKey;
        public XmlDictionaryString RenewTarget;
        public XmlDictionaryString CloseTarget;
        public XmlDictionaryString RequestedTokenClosed;
        public XmlDictionaryString RequestedAttachedReference;
        public XmlDictionaryString RequestedUnattachedReference;
        public XmlDictionaryString IssuedTokensHeader;
        public XmlDictionaryString RequestTypeRenew;
        public XmlDictionaryString RequestTypeClose;

        public TrustDictionary()
        {
        }

        public TrustDictionary(ServiceModelDictionary dictionary)
        {
        }
    }

    internal class TrustApr2004Dictionary : TrustDictionary
    {
        public TrustApr2004Dictionary(ServiceModelDictionary dictionary)
            : base(dictionary)
        {
            CombinedHashLabel = dictionary.CreateString(ServiceModelStringsVersion1.String194, 194);
            RequestSecurityTokenResponse = dictionary.CreateString(ServiceModelStringsVersion1.String195, 195);
            TokenType = dictionary.CreateString(ServiceModelStringsVersion1.String187, 187);
            KeySize = dictionary.CreateString(ServiceModelStringsVersion1.String196, 196);
            RequestedTokenReference = dictionary.CreateString(ServiceModelStringsVersion1.String197, 197);
            AppliesTo = dictionary.CreateString(ServiceModelStringsVersion1.String198, 198);
            Authenticator = dictionary.CreateString(ServiceModelStringsVersion1.String199, 199);
            CombinedHash = dictionary.CreateString(ServiceModelStringsVersion1.String200, 200);
            BinaryExchange = dictionary.CreateString(ServiceModelStringsVersion1.String201, 201);
            Lifetime = dictionary.CreateString(ServiceModelStringsVersion1.String202, 202);
            RequestedSecurityToken = dictionary.CreateString(ServiceModelStringsVersion1.String203, 203);
            Entropy = dictionary.CreateString(ServiceModelStringsVersion1.String204, 204);
            RequestedProofToken = dictionary.CreateString(ServiceModelStringsVersion1.String205, 205);
            ComputedKey = dictionary.CreateString(ServiceModelStringsVersion1.String206, 206);
            RequestSecurityToken = dictionary.CreateString(ServiceModelStringsVersion1.String207, 207);
            RequestType = dictionary.CreateString(ServiceModelStringsVersion1.String208, 208);
            RequestSecurityTokenResponseCollection = dictionary.CreateString(ServiceModelStringsVersion1.String62, 62);
            Context = dictionary.CreateString(ServiceModelStringsVersion1.String209, 209);
            BinarySecret = dictionary.CreateString(ServiceModelStringsVersion1.String210, 210);
            Type = dictionary.CreateString(ServiceModelStringsVersion1.String59, 59);
            SpnegoValueTypeUri = dictionary.CreateString(ServiceModelStringsVersion1.String211, 211);
            TlsnegoValueTypeUri = dictionary.CreateString(ServiceModelStringsVersion1.String212, 212);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String213, 213);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String214, 214);
            RequestSecurityTokenIssuance = dictionary.CreateString(ServiceModelStringsVersion1.String215, 215);
            RequestSecurityTokenIssuanceResponse = dictionary.CreateString(ServiceModelStringsVersion1.String216, 216);
            RequestTypeIssue = dictionary.CreateString(ServiceModelStringsVersion1.String217, 217);
            Psha1ComputedKeyUri = dictionary.CreateString(ServiceModelStringsVersion1.String218, 218);
            SymmetricKeyBinarySecret = dictionary.CreateString(ServiceModelStringsVersion1.String219, 219);
            NonceBinarySecret = dictionary.CreateString(ServiceModelStringsVersion1.String220, 220);
            KeyType = dictionary.CreateString(ServiceModelStringsVersion1.String221, 221);
            SymmetricKeyType = dictionary.CreateString(ServiceModelStringsVersion1.String222, 222);
            PublicKeyType = dictionary.CreateString(ServiceModelStringsVersion1.String223, 223);
            Claims = dictionary.CreateString(ServiceModelStringsVersion1.String224, 224);
            InvalidRequestFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String225, 225);
            FailedAuthenticationFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String182, 182);
            RequestFailedFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String226, 226);
            SignWith = dictionary.CreateString(ServiceModelStringsVersion1.String227, 227);
            EncryptWith = dictionary.CreateString(ServiceModelStringsVersion1.String228, 228);
            EncryptionAlgorithm = dictionary.CreateString(ServiceModelStringsVersion1.String229, 229);
            CanonicalizationAlgorithm = dictionary.CreateString(ServiceModelStringsVersion1.String230, 230);
            ComputedKeyAlgorithm = dictionary.CreateString(ServiceModelStringsVersion1.String231, 231);
            UseKey = dictionary.CreateString(ServiceModelStringsVersion1.String232, 232);
        }
    }

    internal class TrustFeb2005Dictionary : TrustDictionary
    {
        public TrustFeb2005Dictionary(ServiceModelDictionary dictionary)
            : base(dictionary)
        {
            RequestSecurityTokenResponseCollection = dictionary.CreateString(ServiceModelStringsVersion1.String62, 62);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String63, 63);
            BinarySecretClauseType = dictionary.CreateString(ServiceModelStringsVersion1.String64, 64);
            CombinedHashLabel = dictionary.CreateString(ServiceModelStringsVersion1.String194, 194);
            RequestSecurityTokenResponse = dictionary.CreateString(ServiceModelStringsVersion1.String195, 195);
            TokenType = dictionary.CreateString(ServiceModelStringsVersion1.String187, 187);
            KeySize = dictionary.CreateString(ServiceModelStringsVersion1.String196, 196);
            RequestedTokenReference = dictionary.CreateString(ServiceModelStringsVersion1.String197, 197);
            AppliesTo = dictionary.CreateString(ServiceModelStringsVersion1.String198, 198);
            Authenticator = dictionary.CreateString(ServiceModelStringsVersion1.String199, 199);
            CombinedHash = dictionary.CreateString(ServiceModelStringsVersion1.String200, 200);
            BinaryExchange = dictionary.CreateString(ServiceModelStringsVersion1.String201, 201);
            Lifetime = dictionary.CreateString(ServiceModelStringsVersion1.String202, 202);
            RequestedSecurityToken = dictionary.CreateString(ServiceModelStringsVersion1.String203, 203);
            Entropy = dictionary.CreateString(ServiceModelStringsVersion1.String204, 204);
            RequestedProofToken = dictionary.CreateString(ServiceModelStringsVersion1.String205, 205);
            ComputedKey = dictionary.CreateString(ServiceModelStringsVersion1.String206, 206);
            RequestSecurityToken = dictionary.CreateString(ServiceModelStringsVersion1.String207, 207);
            RequestType = dictionary.CreateString(ServiceModelStringsVersion1.String208, 208);
            Context = dictionary.CreateString(ServiceModelStringsVersion1.String209, 209);
            BinarySecret = dictionary.CreateString(ServiceModelStringsVersion1.String210, 210);
            Type = dictionary.CreateString(ServiceModelStringsVersion1.String59, 59);
            SpnegoValueTypeUri = dictionary.CreateString(ServiceModelStringsVersion1.String233, 233);
            TlsnegoValueTypeUri = dictionary.CreateString(ServiceModelStringsVersion1.String234, 234);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String235, 235);
            RequestSecurityTokenIssuance = dictionary.CreateString(ServiceModelStringsVersion1.String236, 236);
            RequestSecurityTokenIssuanceResponse = dictionary.CreateString(ServiceModelStringsVersion1.String237, 237);
            RequestTypeIssue = dictionary.CreateString(ServiceModelStringsVersion1.String238, 238);
            SymmetricKeyBinarySecret = dictionary.CreateString(ServiceModelStringsVersion1.String239, 239);
            Psha1ComputedKeyUri = dictionary.CreateString(ServiceModelStringsVersion1.String240, 240);
            NonceBinarySecret = dictionary.CreateString(ServiceModelStringsVersion1.String241, 241);
            RenewTarget = dictionary.CreateString(ServiceModelStringsVersion1.String242, 242);
            CloseTarget = dictionary.CreateString(ServiceModelStringsVersion1.String243, 243);
            RequestedTokenClosed = dictionary.CreateString(ServiceModelStringsVersion1.String244, 244);
            RequestedAttachedReference = dictionary.CreateString(ServiceModelStringsVersion1.String245, 245);
            RequestedUnattachedReference = dictionary.CreateString(ServiceModelStringsVersion1.String246, 246);
            IssuedTokensHeader = dictionary.CreateString(ServiceModelStringsVersion1.String247, 247);
            RequestTypeRenew = dictionary.CreateString(ServiceModelStringsVersion1.String248, 248);
            RequestTypeClose = dictionary.CreateString(ServiceModelStringsVersion1.String249, 249);
            KeyType = dictionary.CreateString(ServiceModelStringsVersion1.String221, 221);
            SymmetricKeyType = dictionary.CreateString(ServiceModelStringsVersion1.String239, 239);
            PublicKeyType = dictionary.CreateString(ServiceModelStringsVersion1.String250, 250);
            Claims = dictionary.CreateString(ServiceModelStringsVersion1.String224, 224);
            InvalidRequestFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String225, 225);
            FailedAuthenticationFaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String182, 182);
            UseKey = dictionary.CreateString(ServiceModelStringsVersion1.String232, 232);
            SignWith = dictionary.CreateString(ServiceModelStringsVersion1.String227, 227);
            EncryptWith = dictionary.CreateString(ServiceModelStringsVersion1.String228, 228);
            EncryptionAlgorithm = dictionary.CreateString(ServiceModelStringsVersion1.String229, 229);
            CanonicalizationAlgorithm = dictionary.CreateString(ServiceModelStringsVersion1.String230, 230);
            ComputedKeyAlgorithm = dictionary.CreateString(ServiceModelStringsVersion1.String231, 231);
        }
    }

    internal class UtilityDictionary
    {
        public XmlDictionaryString IdAttribute;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString Timestamp;
        public XmlDictionaryString CreatedElement;
        public XmlDictionaryString ExpiresElement;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString UniqueEndpointHeaderName;
        public XmlDictionaryString UniqueEndpointHeaderNamespace;

        public UtilityDictionary(ServiceModelDictionary dictionary)
        {
            IdAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String14, 14);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String51, 51);
            Timestamp = dictionary.CreateString(ServiceModelStringsVersion1.String53, 53);
            CreatedElement = dictionary.CreateString(ServiceModelStringsVersion1.String54, 54);
            ExpiresElement = dictionary.CreateString(ServiceModelStringsVersion1.String55, 55);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String305, 305);
            UniqueEndpointHeaderName = dictionary.CreateString(ServiceModelStringsVersion1.String306, 306);
            UniqueEndpointHeaderNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String307, 307);
        }
    }

    internal class WsrmFeb2005Dictionary
    {
        public XmlDictionaryString Identifier;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString SequenceAcknowledgement;
        public XmlDictionaryString AcknowledgementRange;
        public XmlDictionaryString Upper;
        public XmlDictionaryString Lower;
        public XmlDictionaryString BufferRemaining;
        public XmlDictionaryString NETNamespace;
        public XmlDictionaryString SequenceAcknowledgementAction;
        public XmlDictionaryString Sequence;
        public XmlDictionaryString MessageNumber;
        public XmlDictionaryString AckRequested;
        public XmlDictionaryString AckRequestedAction;
        public XmlDictionaryString AcksTo;
        public XmlDictionaryString Accept;
        public XmlDictionaryString CreateSequence;
        public XmlDictionaryString CreateSequenceAction;
        public XmlDictionaryString CreateSequenceRefused;
        public XmlDictionaryString CreateSequenceResponse;
        public XmlDictionaryString CreateSequenceResponseAction;
        public XmlDictionaryString Expires;
        public XmlDictionaryString FaultCode;
        public XmlDictionaryString InvalidAcknowledgement;
        public XmlDictionaryString LastMessage;
        public XmlDictionaryString LastMessageAction;
        public XmlDictionaryString LastMessageNumberExceeded;
        public XmlDictionaryString MessageNumberRollover;
        public XmlDictionaryString Nack;
        public XmlDictionaryString NETPrefix;
        public XmlDictionaryString Offer;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString SequenceFault;
        public XmlDictionaryString SequenceTerminated;
        public XmlDictionaryString TerminateSequence;
        public XmlDictionaryString TerminateSequenceAction;
        public XmlDictionaryString UnknownSequence;
        public XmlDictionaryString ConnectionLimitReached;

        public WsrmFeb2005Dictionary(ServiceModelDictionary dictionary)
        {
            Identifier = dictionary.CreateString(ServiceModelStringsVersion1.String15, 15);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String16, 16);
            SequenceAcknowledgement = dictionary.CreateString(ServiceModelStringsVersion1.String23, 23);
            AcknowledgementRange = dictionary.CreateString(ServiceModelStringsVersion1.String24, 24);
            Upper = dictionary.CreateString(ServiceModelStringsVersion1.String25, 25);
            Lower = dictionary.CreateString(ServiceModelStringsVersion1.String26, 26);
            BufferRemaining = dictionary.CreateString(ServiceModelStringsVersion1.String27, 27);
            NETNamespace = dictionary.CreateString(ServiceModelStringsVersion1.String28, 28);
            SequenceAcknowledgementAction = dictionary.CreateString(ServiceModelStringsVersion1.String29, 29);
            Sequence = dictionary.CreateString(ServiceModelStringsVersion1.String31, 31);
            MessageNumber = dictionary.CreateString(ServiceModelStringsVersion1.String32, 32);
            AckRequested = dictionary.CreateString(ServiceModelStringsVersion1.String328, 328);
            AckRequestedAction = dictionary.CreateString(ServiceModelStringsVersion1.String329, 329);
            AcksTo = dictionary.CreateString(ServiceModelStringsVersion1.String330, 330);
            Accept = dictionary.CreateString(ServiceModelStringsVersion1.String331, 331);
            CreateSequence = dictionary.CreateString(ServiceModelStringsVersion1.String332, 332);
            CreateSequenceAction = dictionary.CreateString(ServiceModelStringsVersion1.String333, 333);
            CreateSequenceRefused = dictionary.CreateString(ServiceModelStringsVersion1.String334, 334);
            CreateSequenceResponse = dictionary.CreateString(ServiceModelStringsVersion1.String335, 335);
            CreateSequenceResponseAction = dictionary.CreateString(ServiceModelStringsVersion1.String336, 336);
            Expires = dictionary.CreateString(ServiceModelStringsVersion1.String55, 55);
            FaultCode = dictionary.CreateString(ServiceModelStringsVersion1.String337, 337);
            InvalidAcknowledgement = dictionary.CreateString(ServiceModelStringsVersion1.String338, 338);
            LastMessage = dictionary.CreateString(ServiceModelStringsVersion1.String339, 339);
            LastMessageAction = dictionary.CreateString(ServiceModelStringsVersion1.String340, 340);
            LastMessageNumberExceeded = dictionary.CreateString(ServiceModelStringsVersion1.String341, 341);
            MessageNumberRollover = dictionary.CreateString(ServiceModelStringsVersion1.String342, 342);
            Nack = dictionary.CreateString(ServiceModelStringsVersion1.String343, 343);
            NETPrefix = dictionary.CreateString(ServiceModelStringsVersion1.String344, 344);
            Offer = dictionary.CreateString(ServiceModelStringsVersion1.String345, 345);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String346, 346);
            SequenceFault = dictionary.CreateString(ServiceModelStringsVersion1.String347, 347);
            SequenceTerminated = dictionary.CreateString(ServiceModelStringsVersion1.String348, 348);
            TerminateSequence = dictionary.CreateString(ServiceModelStringsVersion1.String349, 349);
            TerminateSequenceAction = dictionary.CreateString(ServiceModelStringsVersion1.String350, 350);
            UnknownSequence = dictionary.CreateString(ServiceModelStringsVersion1.String351, 351);
            ConnectionLimitReached = dictionary.CreateString(ServiceModelStringsVersion1.String480, 480);
        }
    }

    internal class XmlEncryptionDictionary
    {
        public XmlDictionaryString Namespace;
        public XmlDictionaryString DataReference;
        public XmlDictionaryString EncryptedData;
        public XmlDictionaryString EncryptionMethod;
        public XmlDictionaryString CipherData;
        public XmlDictionaryString CipherValue;
        public XmlDictionaryString ReferenceList;
        public XmlDictionaryString Encoding;
        public XmlDictionaryString MimeType;
        public XmlDictionaryString Type;
        public XmlDictionaryString Id;
        public XmlDictionaryString CarriedKeyName;
        public XmlDictionaryString Recipient;
        public XmlDictionaryString EncryptedKey;
        public XmlDictionaryString URI;
        public XmlDictionaryString KeyReference;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString ElementType;
        public XmlDictionaryString ContentType;
        public XmlDictionaryString AlgorithmAttribute;

        public XmlEncryptionDictionary(ServiceModelDictionary dictionary)
        {
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String37, 37);
            DataReference = dictionary.CreateString(ServiceModelStringsVersion1.String46, 46);
            EncryptedData = dictionary.CreateString(ServiceModelStringsVersion1.String47, 47);
            EncryptionMethod = dictionary.CreateString(ServiceModelStringsVersion1.String48, 48);
            CipherData = dictionary.CreateString(ServiceModelStringsVersion1.String49, 49);
            CipherValue = dictionary.CreateString(ServiceModelStringsVersion1.String50, 50);
            ReferenceList = dictionary.CreateString(ServiceModelStringsVersion1.String57, 57);
            Encoding = dictionary.CreateString(ServiceModelStringsVersion1.String308, 308);
            MimeType = dictionary.CreateString(ServiceModelStringsVersion1.String309, 309);
            Type = dictionary.CreateString(ServiceModelStringsVersion1.String59, 59);
            Id = dictionary.CreateString(ServiceModelStringsVersion1.String14, 14);
            CarriedKeyName = dictionary.CreateString(ServiceModelStringsVersion1.String310, 310);
            Recipient = dictionary.CreateString(ServiceModelStringsVersion1.String311, 311);
            EncryptedKey = dictionary.CreateString(ServiceModelStringsVersion1.String312, 312);
            URI = dictionary.CreateString(ServiceModelStringsVersion1.String11, 11);
            KeyReference = dictionary.CreateString(ServiceModelStringsVersion1.String313, 313);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String314, 314);
            ElementType = dictionary.CreateString(ServiceModelStringsVersion1.String315, 315);
            ContentType = dictionary.CreateString(ServiceModelStringsVersion1.String316, 316);
            AlgorithmAttribute = dictionary.CreateString(ServiceModelStringsVersion1.String8, 8);
        }
    }

    internal class XmlSignatureDictionary
    {
        public XmlDictionaryString Algorithm;
        public XmlDictionaryString URI;
        public XmlDictionaryString Reference;
        public XmlDictionaryString Transforms;
        public XmlDictionaryString Transform;
        public XmlDictionaryString DigestMethod;
        public XmlDictionaryString DigestValue;
        public XmlDictionaryString Namespace;
        public XmlDictionaryString EnvelopedSignature;
        public XmlDictionaryString KeyInfo;
        public XmlDictionaryString Signature;
        public XmlDictionaryString SignedInfo;
        public XmlDictionaryString CanonicalizationMethod;
        public XmlDictionaryString SignatureMethod;
        public XmlDictionaryString SignatureValue;
        public XmlDictionaryString KeyName;
        public XmlDictionaryString Type;
        public XmlDictionaryString MgmtData;
        public XmlDictionaryString Prefix;
        public XmlDictionaryString KeyValue;
        public XmlDictionaryString RsaKeyValue;
        public XmlDictionaryString Modulus;
        public XmlDictionaryString Exponent;
        public XmlDictionaryString X509Data;
        public XmlDictionaryString X509IssuerSerial;
        public XmlDictionaryString X509IssuerName;
        public XmlDictionaryString X509SerialNumber;
        public XmlDictionaryString X509Certificate;

        public XmlSignatureDictionary(ServiceModelDictionary dictionary)
        {
            Algorithm = dictionary.CreateString(ServiceModelStringsVersion1.String8, 8);
            URI = dictionary.CreateString(ServiceModelStringsVersion1.String11, 11);
            Reference = dictionary.CreateString(ServiceModelStringsVersion1.String12, 12);
            Transforms = dictionary.CreateString(ServiceModelStringsVersion1.String17, 17);
            Transform = dictionary.CreateString(ServiceModelStringsVersion1.String18, 18);
            DigestMethod = dictionary.CreateString(ServiceModelStringsVersion1.String19, 19);
            DigestValue = dictionary.CreateString(ServiceModelStringsVersion1.String20, 20);
            Namespace = dictionary.CreateString(ServiceModelStringsVersion1.String33, 33);
            EnvelopedSignature = dictionary.CreateString(ServiceModelStringsVersion1.String34, 34);
            KeyInfo = dictionary.CreateString(ServiceModelStringsVersion1.String35, 35);
            Signature = dictionary.CreateString(ServiceModelStringsVersion1.String41, 41);
            SignedInfo = dictionary.CreateString(ServiceModelStringsVersion1.String42, 42);
            CanonicalizationMethod = dictionary.CreateString(ServiceModelStringsVersion1.String43, 43);
            SignatureMethod = dictionary.CreateString(ServiceModelStringsVersion1.String44, 44);
            SignatureValue = dictionary.CreateString(ServiceModelStringsVersion1.String45, 45);
            KeyName = dictionary.CreateString(ServiceModelStringsVersion1.String317, 317);
            Type = dictionary.CreateString(ServiceModelStringsVersion1.String59, 59);
            MgmtData = dictionary.CreateString(ServiceModelStringsVersion1.String318, 318);
            Prefix = dictionary.CreateString(ServiceModelStringsVersion1.String81, 81);
            KeyValue = dictionary.CreateString(ServiceModelStringsVersion1.String319, 319);
            RsaKeyValue = dictionary.CreateString(ServiceModelStringsVersion1.String320, 320);
            Modulus = dictionary.CreateString(ServiceModelStringsVersion1.String321, 321);
            Exponent = dictionary.CreateString(ServiceModelStringsVersion1.String322, 322);
            X509Data = dictionary.CreateString(ServiceModelStringsVersion1.String323, 323);
            X509IssuerSerial = dictionary.CreateString(ServiceModelStringsVersion1.String324, 324);
            X509IssuerName = dictionary.CreateString(ServiceModelStringsVersion1.String325, 325);
            X509SerialNumber = dictionary.CreateString(ServiceModelStringsVersion1.String326, 326);
            X509Certificate = dictionary.CreateString(ServiceModelStringsVersion1.String327, 327);
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

    internal static class ActivityIdFlowStrings
    {
        // Main dictionary strings
        public const string ActivityId = ServiceModelStringsVersion1.String425;
        public const string ActivityIdNamespace = ServiceModelStringsVersion1.String426;
    }

    internal static class AddressingStrings
    {
        // Main dictionary strings
        public const string Action = ServiceModelStringsVersion1.String5;
        public const string To = ServiceModelStringsVersion1.String6;
        public const string RelatesTo = ServiceModelStringsVersion1.String9;
        public const string MessageId = ServiceModelStringsVersion1.String13;
        public const string Address = ServiceModelStringsVersion1.String21;
        public const string ReplyTo = ServiceModelStringsVersion1.String22;
        public const string Empty = ServiceModelStringsVersion1.String81;
        public const string From = ServiceModelStringsVersion1.String82;
        public const string FaultTo = ServiceModelStringsVersion1.String83;
        public const string EndpointReference = ServiceModelStringsVersion1.String84;
        public const string PortType = ServiceModelStringsVersion1.String85;
        public const string ServiceName = ServiceModelStringsVersion1.String86;
        public const string PortName = ServiceModelStringsVersion1.String87;
        public const string ReferenceProperties = ServiceModelStringsVersion1.String88;
        public const string RelationshipType = ServiceModelStringsVersion1.String89;
        public const string Reply = ServiceModelStringsVersion1.String90;
        public const string Prefix = ServiceModelStringsVersion1.String91;
        public const string IdentityExtensionNamespace = ServiceModelStringsVersion1.String92;
        public const string Identity = ServiceModelStringsVersion1.String93;
        public const string Spn = ServiceModelStringsVersion1.String94;
        public const string Upn = ServiceModelStringsVersion1.String95;
        public const string Rsa = ServiceModelStringsVersion1.String96;
        public const string Dns = ServiceModelStringsVersion1.String97;
        public const string X509v3Certificate = ServiceModelStringsVersion1.String98;
        public const string ReferenceParameters = ServiceModelStringsVersion1.String100;
        public const string IsReferenceParameter = ServiceModelStringsVersion1.String101;
        // String constants
        public const string EndpointUnavailable = "EndpointUnavailable";
        public const string ActionNotSupported = "ActionNotSupported";
        public const string EndpointReferenceType = "EndpointReferenceType";
        public const string Request = "Request";
        public const string DestinationUnreachable = "DestinationUnreachable";
        public const string AnonymousUri = "http://schemas.microsoft.com/2005/12/ServiceModel/Addressing/Anonymous";
        public const string NoneUri = "http://schemas.microsoft.com/2005/12/ServiceModel/Addressing/None";
        public const string IndigoNamespace = "http://schemas.microsoft.com/serviceModel/2004/05/addressing";
        public const string ChannelTerminated = "ChannelTerminated";
    }

    internal static class Addressing10Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String3;
        public const string Anonymous = ServiceModelStringsVersion1.String10;
        public const string FaultAction = ServiceModelStringsVersion1.String99;
        public const string ReplyRelationship = ServiceModelStringsVersion1.String102;
        public const string NoneAddress = ServiceModelStringsVersion1.String103;
        public const string Metadata = ServiceModelStringsVersion1.String104;
        // String constants
        public const string MessageAddressingHeaderRequired = "MessageAddressingHeaderRequired";
        public const string InvalidAddressingHeader = "InvalidAddressingHeader";
        public const string InvalidCardinality = "InvalidCardinality";
        public const string ActionMismatch = "ActionMismatch";
        public const string ProblemHeaderQName = "ProblemHeaderQName";
        public const string FaultDetail = "FaultDetail";
        public const string DefaultFaultAction = "http://www.w3.org/2005/08/addressing/soap/fault";
    }

    internal static class Addressing200408Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String105;
        public const string Anonymous = ServiceModelStringsVersion1.String106;
        public const string FaultAction = ServiceModelStringsVersion1.String107;
        // String constants
        public const string InvalidMessageInformationHeader = "InvalidMessageInformationHeader";
        public const string MessageInformationHeaderRequired = "MessageInformationHeaderRequired";
        public const string DefaultFaultAction = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault";
    }

    internal static class AddressingNoneStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String439;
    }

    internal static class AtomicTransactionExternalStrings
    {
        // Main dictionary strings
        public const string Prefix = ServiceModelStringsVersion1.String383;
        public const string Prepare = ServiceModelStringsVersion1.String387;
        public const string Prepared = ServiceModelStringsVersion1.String388;
        public const string ReadOnly = ServiceModelStringsVersion1.String389;
        public const string Commit = ServiceModelStringsVersion1.String390;
        public const string Rollback = ServiceModelStringsVersion1.String391;
        public const string Committed = ServiceModelStringsVersion1.String392;
        public const string Aborted = ServiceModelStringsVersion1.String393;
        public const string Replay = ServiceModelStringsVersion1.String394;
        public const string CompletionCoordinatorPortType = ServiceModelStringsVersion1.String404;
        public const string CompletionParticipantPortType = ServiceModelStringsVersion1.String405;
        public const string CoordinatorPortType = ServiceModelStringsVersion1.String406;
        public const string ParticipantPortType = ServiceModelStringsVersion1.String407;
        public const string InconsistentInternalState = ServiceModelStringsVersion1.String408;
    }

    internal static class AtomicTransactionExternal10Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String382;
        public const string CompletionUri = ServiceModelStringsVersion1.String384;
        public const string Durable2PCUri = ServiceModelStringsVersion1.String385;
        public const string Volatile2PCUri = ServiceModelStringsVersion1.String386;
        public const string CommitAction = ServiceModelStringsVersion1.String395;
        public const string RollbackAction = ServiceModelStringsVersion1.String396;
        public const string CommittedAction = ServiceModelStringsVersion1.String397;
        public const string AbortedAction = ServiceModelStringsVersion1.String398;
        public const string PrepareAction = ServiceModelStringsVersion1.String399;
        public const string PreparedAction = ServiceModelStringsVersion1.String400;
        public const string ReadOnlyAction = ServiceModelStringsVersion1.String401;
        public const string ReplayAction = ServiceModelStringsVersion1.String402;
        public const string FaultAction = ServiceModelStringsVersion1.String403;
    }

    internal static class CoordinationExternalStrings
    {
        // Main dictionary strings
        public const string Prefix = ServiceModelStringsVersion1.String357;
        public const string CreateCoordinationContext = ServiceModelStringsVersion1.String358;
        public const string CreateCoordinationContextResponse = ServiceModelStringsVersion1.String359;
        public const string CoordinationContext = ServiceModelStringsVersion1.String360;
        public const string CurrentContext = ServiceModelStringsVersion1.String361;
        public const string CoordinationType = ServiceModelStringsVersion1.String362;
        public const string RegistrationService = ServiceModelStringsVersion1.String363;
        public const string Register = ServiceModelStringsVersion1.String364;
        public const string RegisterResponse = ServiceModelStringsVersion1.String365;
        public const string Protocol = ServiceModelStringsVersion1.String366;
        public const string CoordinatorProtocolService = ServiceModelStringsVersion1.String367;
        public const string ParticipantProtocolService = ServiceModelStringsVersion1.String368;
        public const string Expires = ServiceModelStringsVersion1.String55;
        public const string Identifier = ServiceModelStringsVersion1.String15;
        public const string ActivationCoordinatorPortType = ServiceModelStringsVersion1.String374;
        public const string RegistrationCoordinatorPortType = ServiceModelStringsVersion1.String375;
        public const string InvalidState = ServiceModelStringsVersion1.String376;
        public const string InvalidProtocol = ServiceModelStringsVersion1.String377;
        public const string InvalidParameters = ServiceModelStringsVersion1.String378;
        public const string NoActivity = ServiceModelStringsVersion1.String379;
        public const string ContextRefused = ServiceModelStringsVersion1.String380;
        public const string AlreadyRegistered = ServiceModelStringsVersion1.String381;
    }

    internal static class CoordinationExternal10Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String356;
        public const string CreateCoordinationContextAction = ServiceModelStringsVersion1.String369;
        public const string CreateCoordinationContextResponseAction = ServiceModelStringsVersion1.String370;
        public const string RegisterAction = ServiceModelStringsVersion1.String371;
        public const string RegisterResponseAction = ServiceModelStringsVersion1.String372;
        public const string FaultAction = ServiceModelStringsVersion1.String373;
    }

    internal static class DotNetAddressingStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String108;
        public const string RedirectTo = ServiceModelStringsVersion1.String109;
        public const string Via = ServiceModelStringsVersion1.String110;
    }

    internal static class DotNetAtomicTransactionExternalStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String65;
        public const string Prefix = ServiceModelStringsVersion1.String409;
        public const string Enlistment = ServiceModelStringsVersion1.String410;
        public const string Protocol = ServiceModelStringsVersion1.String411;
        public const string LocalTransactionId = ServiceModelStringsVersion1.String412;
        public const string IsolationLevel = ServiceModelStringsVersion1.String413;
        public const string IsolationFlags = ServiceModelStringsVersion1.String414;
        public const string Description = ServiceModelStringsVersion1.String415;
        public const string Loopback = ServiceModelStringsVersion1.String416;
        public const string RegisterInfo = ServiceModelStringsVersion1.String417;
        public const string ContextId = ServiceModelStringsVersion1.String418;
        public const string TokenId = ServiceModelStringsVersion1.String419;
        public const string AccessDenied = ServiceModelStringsVersion1.String420;
        public const string InvalidPolicy = ServiceModelStringsVersion1.String421;
        public const string CoordinatorRegistrationFailed = ServiceModelStringsVersion1.String422;
        public const string TooManyEnlistments = ServiceModelStringsVersion1.String423;
        public const string Disabled = ServiceModelStringsVersion1.String424;
    }

    internal static class DotNetOneWayStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String437;
        public const string HeaderName = ServiceModelStringsVersion1.String438;
    }

    internal static class DotNetSecurityStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String162;
        public const string Prefix = ServiceModelStringsVersion1.String163;
        // String constants
        public const string KeyRenewalNeededFault = "ExpiredSecurityContextTokenKey";
        public const string SecuritySessionAbortedFault = "SecuritySessionAborted";
        public const string SecurityServerTooBusyFault = "ServerTooBusy";
        public const string SecuritySessionFaultAction = "http://schemas.microsoft.com/ws/2006/05/security/SecureConversationFault";
        public const string SecureConversationCancelNotAllowedFault = "SecureConversationCancellationNotAllowed";
    }

    internal static class ExclusiveC14NStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String111;
        public const string PrefixList = ServiceModelStringsVersion1.String112;
        public const string InclusiveNamespaces = ServiceModelStringsVersion1.String113;
        public const string Prefix = ServiceModelStringsVersion1.String114;
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

    internal static class Message11Strings
    {
        // Text dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String481;
        public const string Actor = ServiceModelStringsVersion1.String482;
        public const string FaultCode = ServiceModelStringsVersion1.String483;
        public const string FaultString = ServiceModelStringsVersion1.String484;
        public const string FaultActor = ServiceModelStringsVersion1.String485;
        public const string FaultDetail = ServiceModelStringsVersion1.String486;
        public const string FaultNamespace = ServiceModelStringsVersion1.String81;
    }

    internal static class Message12Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String2;
        public const string Role = ServiceModelStringsVersion1.String69;
        public const string Relay = ServiceModelStringsVersion1.String70;
        public const string FaultCode = ServiceModelStringsVersion1.String71;
        public const string FaultReason = ServiceModelStringsVersion1.String72;
        public const string FaultText = ServiceModelStringsVersion1.String73;
        public const string FaultNode = ServiceModelStringsVersion1.String74;
        public const string FaultRole = ServiceModelStringsVersion1.String75;
        public const string FaultDetail = ServiceModelStringsVersion1.String76;
        public const string FaultValue = ServiceModelStringsVersion1.String77;
        public const string FaultSubcode = ServiceModelStringsVersion1.String78;
        public const string NotUnderstood = ServiceModelStringsVersion1.String79;
        public const string QName = ServiceModelStringsVersion1.String80;
    }

    internal static class OleTxTransactionExternalStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String352;
        public const string Prefix = ServiceModelStringsVersion1.String353;
        public const string OleTxTransaction = ServiceModelStringsVersion1.String354;
        public const string PropagationToken = ServiceModelStringsVersion1.String355;
    }

    internal static class PeerWireStringsStrings
    {
        // Main dictionary strings
        public const string FloodAction = ServiceModelStringsVersion1.String429;
        public const string LinkUtilityAction = ServiceModelStringsVersion1.String430;
        public const string HopCount = ServiceModelStringsVersion1.String431;
        public const string HopCountNamespace = ServiceModelStringsVersion1.String432;
        public const string PeerVia = ServiceModelStringsVersion1.String433;
        public const string Namespace = ServiceModelStringsVersion1.String434;
        public const string Demuxer = ServiceModelStringsVersion1.String435;
        public const string PeerTo = ServiceModelStringsVersion1.String436;
    }

    internal static class PolicyStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String428;
    }

    internal static class SamlStrings
    {
        // Main dictionary strings
        public const string Access = ServiceModelStringsVersion1.String251;
        public const string AccessDecision = ServiceModelStringsVersion1.String252;
        public const string Action = ServiceModelStringsVersion1.String5;
        public const string Advice = ServiceModelStringsVersion1.String253;
        public const string Assertion = ServiceModelStringsVersion1.String179;
        public const string AssertionId = ServiceModelStringsVersion1.String254;
        public const string AssertionIdReference = ServiceModelStringsVersion1.String255;
        public const string Attribute = ServiceModelStringsVersion1.String256;
        public const string AttributeName = ServiceModelStringsVersion1.String257;
        public const string AttributeNamespace = ServiceModelStringsVersion1.String258;
        public const string AttributeStatement = ServiceModelStringsVersion1.String259;
        public const string AttributeValue = ServiceModelStringsVersion1.String260;
        public const string Audience = ServiceModelStringsVersion1.String261;
        public const string AudienceRestrictionCondition = ServiceModelStringsVersion1.String262;
        public const string AuthenticationInstant = ServiceModelStringsVersion1.String263;
        public const string AuthenticationMethod = ServiceModelStringsVersion1.String264;
        public const string AuthenticationStatement = ServiceModelStringsVersion1.String265;
        public const string AuthorityBinding = ServiceModelStringsVersion1.String266;
        public const string AuthorityKind = ServiceModelStringsVersion1.String267;
        public const string AuthorizationDecisionStatement = ServiceModelStringsVersion1.String268;
        public const string Binding = ServiceModelStringsVersion1.String269;
        public const string Condition = ServiceModelStringsVersion1.String270;
        public const string Conditions = ServiceModelStringsVersion1.String271;
        public const string Decision = ServiceModelStringsVersion1.String272;
        public const string DoNotCacheCondition = ServiceModelStringsVersion1.String273;
        public const string Evidence = ServiceModelStringsVersion1.String274;
        public const string IssueInstant = ServiceModelStringsVersion1.String275;
        public const string Issuer = ServiceModelStringsVersion1.String276;
        public const string Location = ServiceModelStringsVersion1.String277;
        public const string MajorVersion = ServiceModelStringsVersion1.String278;
        public const string MinorVersion = ServiceModelStringsVersion1.String279;
        public const string Namespace = ServiceModelStringsVersion1.String180;
        public const string NameIdentifier = ServiceModelStringsVersion1.String280;
        public const string NameIdentifierFormat = ServiceModelStringsVersion1.String281;
        public const string NameIdentifierNameQualifier = ServiceModelStringsVersion1.String282;
        public const string ActionNamespaceAttribute = ServiceModelStringsVersion1.String283;
        public const string NotBefore = ServiceModelStringsVersion1.String284;
        public const string NotOnOrAfter = ServiceModelStringsVersion1.String285;
        public const string PreferredPrefix = ServiceModelStringsVersion1.String286;
        public const string Statement = ServiceModelStringsVersion1.String287;
        public const string Subject = ServiceModelStringsVersion1.String288;
        public const string SubjectConfirmation = ServiceModelStringsVersion1.String289;
        public const string SubjectConfirmationData = ServiceModelStringsVersion1.String290;
        public const string SubjectConfirmationMethod = ServiceModelStringsVersion1.String291;
        public const string HolderOfKey = ServiceModelStringsVersion1.String292;
        public const string SenderVouches = ServiceModelStringsVersion1.String293;
        public const string SubjectLocality = ServiceModelStringsVersion1.String294;
        public const string SubjectLocalityDNSAddress = ServiceModelStringsVersion1.String295;
        public const string SubjectLocalityIPAddress = ServiceModelStringsVersion1.String296;
        public const string SubjectStatement = ServiceModelStringsVersion1.String297;
        public const string UnspecifiedAuthenticationMethod = ServiceModelStringsVersion1.String298;
        public const string NamespaceAttributePrefix = ServiceModelStringsVersion1.String299;
        public const string Resource = ServiceModelStringsVersion1.String300;
        public const string UserName = ServiceModelStringsVersion1.String301;
        public const string UserNameNamespace = ServiceModelStringsVersion1.String302;
        public const string EmailName = ServiceModelStringsVersion1.String303;
        public const string EmailNamespace = ServiceModelStringsVersion1.String304;
    }

    internal static class SecureConversationStrings
    {
    }

    internal static class SecureConversationApr2004Strings
    {
        // Main dictionary strings
        public const string SecurityContextToken = ServiceModelStringsVersion1.String115;
        public const string DerivedKeyToken = ServiceModelStringsVersion1.String39;
        public const string AlgorithmAttribute = ServiceModelStringsVersion1.String8;
        public const string Generation = ServiceModelStringsVersion1.String116;
        public const string Label = ServiceModelStringsVersion1.String117;
        public const string Length = ServiceModelStringsVersion1.String56;
        public const string Nonce = ServiceModelStringsVersion1.String40;
        public const string Offset = ServiceModelStringsVersion1.String118;
        public const string Properties = ServiceModelStringsVersion1.String119;
        public const string Identifier = ServiceModelStringsVersion1.String15;
        public const string Cookie = ServiceModelStringsVersion1.String120;
        public const string Prefix = ServiceModelStringsVersion1.String121;
        public const string Namespace = ServiceModelStringsVersion1.String122;
        public const string DerivedKeyTokenType = ServiceModelStringsVersion1.String123;
        public const string SecurityContextTokenType = ServiceModelStringsVersion1.String124;
        public const string SecurityContextTokenReferenceValueType = ServiceModelStringsVersion1.String124;
        public const string RequestSecurityContextIssuance = ServiceModelStringsVersion1.String125;
        public const string RequestSecurityContextIssuanceResponse = ServiceModelStringsVersion1.String126;
        public const string RenewNeededFaultCode = ServiceModelStringsVersion1.String127;
        public const string BadContextTokenFaultCode = ServiceModelStringsVersion1.String128;
    }

    internal static class SecureConversationFeb2005Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String38;
        public const string DerivedKeyToken = ServiceModelStringsVersion1.String39;
        public const string Nonce = ServiceModelStringsVersion1.String40;
        public const string Length = ServiceModelStringsVersion1.String56;
        public const string SecurityContextToken = ServiceModelStringsVersion1.String115;
        public const string AlgorithmAttribute = ServiceModelStringsVersion1.String8;
        public const string Generation = ServiceModelStringsVersion1.String116;
        public const string Label = ServiceModelStringsVersion1.String117;
        public const string Offset = ServiceModelStringsVersion1.String118;
        public const string Properties = ServiceModelStringsVersion1.String119;
        public const string Identifier = ServiceModelStringsVersion1.String15;
        public const string Cookie = ServiceModelStringsVersion1.String120;
        public const string RenewNeededFaultCode = ServiceModelStringsVersion1.String127;
        public const string BadContextTokenFaultCode = ServiceModelStringsVersion1.String128;
        public const string Prefix = ServiceModelStringsVersion1.String129;
        public const string DerivedKeyTokenType = ServiceModelStringsVersion1.String130;
        public const string SecurityContextTokenType = ServiceModelStringsVersion1.String131;
        public const string SecurityContextTokenReferenceValueType = ServiceModelStringsVersion1.String131;
        public const string RequestSecurityContextIssuance = ServiceModelStringsVersion1.String132;
        public const string RequestSecurityContextIssuanceResponse = ServiceModelStringsVersion1.String133;
        public const string RequestSecurityContextRenew = ServiceModelStringsVersion1.String134;
        public const string RequestSecurityContextRenewResponse = ServiceModelStringsVersion1.String135;
        public const string RequestSecurityContextClose = ServiceModelStringsVersion1.String136;
        public const string RequestSecurityContextCloseResponse = ServiceModelStringsVersion1.String137;
    }

    internal static class SecurityAlgorithmStrings
    {
        // Main dictionary strings
        public const string Aes128Encryption = ServiceModelStringsVersion1.String138;
        public const string Aes128KeyWrap = ServiceModelStringsVersion1.String139;
        public const string Aes192Encryption = ServiceModelStringsVersion1.String140;
        public const string Aes192KeyWrap = ServiceModelStringsVersion1.String141;
        public const string Aes256Encryption = ServiceModelStringsVersion1.String142;
        public const string Aes256KeyWrap = ServiceModelStringsVersion1.String143;
        public const string DesEncryption = ServiceModelStringsVersion1.String144;
        public const string DsaSha1Signature = ServiceModelStringsVersion1.String145;
        public const string ExclusiveC14n = ServiceModelStringsVersion1.String111;
        public const string ExclusiveC14nWithComments = ServiceModelStringsVersion1.String146;
        public const string HmacSha1Signature = ServiceModelStringsVersion1.String147;
        public const string HmacSha256Signature = ServiceModelStringsVersion1.String148;
        public const string Psha1KeyDerivation = ServiceModelStringsVersion1.String149;
        public const string Ripemd160Digest = ServiceModelStringsVersion1.String150;
        public const string RsaOaepKeyWrap = ServiceModelStringsVersion1.String151;
        public const string RsaSha1Signature = ServiceModelStringsVersion1.String152;
        public const string RsaSha256Signature = ServiceModelStringsVersion1.String153;
        public const string RsaV15KeyWrap = ServiceModelStringsVersion1.String154;
        public const string Sha1Digest = ServiceModelStringsVersion1.String155;
        public const string Sha256Digest = ServiceModelStringsVersion1.String156;
        public const string Sha512Digest = ServiceModelStringsVersion1.String157;
        public const string TripleDesEncryption = ServiceModelStringsVersion1.String158;
        public const string TripleDesKeyWrap = ServiceModelStringsVersion1.String159;
        public const string TlsSspiKeyWrap = ServiceModelStringsVersion1.String160;
        public const string WindowsSspiKeyWrap = ServiceModelStringsVersion1.String161;
        // String constants
        public const string StrTransform = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#STR-Transform";
    }

    internal static class SecurityJan2004Strings
    {
        // Main dictionary strings
        public const string SecurityTokenReference = ServiceModelStringsVersion1.String30;
        public const string Namespace = ServiceModelStringsVersion1.String36;
        public const string Security = ServiceModelStringsVersion1.String52;
        public const string ValueType = ServiceModelStringsVersion1.String58;
        public const string TypeAttribute = ServiceModelStringsVersion1.String59;
        public const string Prefix = ServiceModelStringsVersion1.String164;
        public const string NonceElement = ServiceModelStringsVersion1.String40;
        public const string PasswordElement = ServiceModelStringsVersion1.String165;
        public const string PasswordTextName = ServiceModelStringsVersion1.String166;
        public const string UserNameElement = ServiceModelStringsVersion1.String167;
        public const string UserNameTokenElement = ServiceModelStringsVersion1.String168;
        public const string BinarySecurityToken = ServiceModelStringsVersion1.String169;
        public const string EncodingType = ServiceModelStringsVersion1.String170;
        public const string Reference = ServiceModelStringsVersion1.String12;
        public const string URI = ServiceModelStringsVersion1.String11;
        public const string KeyIdentifier = ServiceModelStringsVersion1.String171;
        public const string EncodingTypeValueBase64Binary = ServiceModelStringsVersion1.String172;
        public const string EncodingTypeValueHexBinary = ServiceModelStringsVersion1.String173;
        public const string EncodingTypeValueText = ServiceModelStringsVersion1.String174;
        public const string X509SKIValueType = ServiceModelStringsVersion1.String175;
        public const string KerberosTokenTypeGSS = ServiceModelStringsVersion1.String176;
        public const string KerberosTokenType1510 = ServiceModelStringsVersion1.String177;
        public const string SamlAssertionIdValueType = ServiceModelStringsVersion1.String178;
        public const string SamlAssertion = ServiceModelStringsVersion1.String179;
        public const string SamlUri = ServiceModelStringsVersion1.String180;
        public const string RelAssertionValueType = ServiceModelStringsVersion1.String181;
        public const string FailedAuthenticationFaultCode = ServiceModelStringsVersion1.String182;
        public const string InvalidSecurityTokenFaultCode = ServiceModelStringsVersion1.String183;
        public const string InvalidSecurityFaultCode = ServiceModelStringsVersion1.String184;
        public const string KerberosHashValueType = ServiceModelStringsVersion1.String427;
        // String constants
        public const string SecurityProfileNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0";
        public const string X509TokenProfileNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0";
        public const string UPTokenProfileNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0";
        public const string SamlTokenProfileNamespace = "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.0";
        public const string KerberosTokenProfileNamespace = "http://www.docs.oasis-open.org/wss/2004/07/oasis-000000-wss-kerberos-token-profile-1.0";
        public const string UPTokenType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#UsernameToken";
        public const string X509TokenType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";
        public const string UPTokenPasswordTextValue = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";
    }

    internal static class SecurityXXX2005Strings
    {
        // Main dictionary strings
        public const string EncryptedHeader = ServiceModelStringsVersion1.String60;
        public const string Namespace = ServiceModelStringsVersion1.String61;
        public const string Prefix = ServiceModelStringsVersion1.String185;
        public const string SignatureConfirmation = ServiceModelStringsVersion1.String186;
        public const string ValueAttribute = ServiceModelStringsVersion1.String77;
        public const string TokenTypeAttribute = ServiceModelStringsVersion1.String187;
        public const string ThumbprintSha1ValueType = ServiceModelStringsVersion1.String188;
        public const string EncryptedKeyTokenType = ServiceModelStringsVersion1.String189;
        public const string EncryptedKeyHashValueType = ServiceModelStringsVersion1.String190;
        public const string SamlTokenType = ServiceModelStringsVersion1.String191;
        public const string Saml20TokenType = ServiceModelStringsVersion1.String192;
        public const string Saml11AssertionValueType = ServiceModelStringsVersion1.String193;
        // String constants
        public const string SecurityProfileNamespace = "http://docs.oasis-open.org/wss/oasis-wss-soap-message-security-1.1";
        public const string SamlTokenProfileNamespace = "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1";
        public const string X509TokenProfileNamespace = "http://docs.oasis-open.org/wss/2004/xx/oasis-2004xx-wss-x509-token-profile-1.1";
    }

    internal static class SerializationStrings
    {
        // Main dictionary strings
        public const string XmlSchemaInstanceNamespace = ServiceModelStringsVersion1.String441;
        public const string XmlSchemaNamespace = ServiceModelStringsVersion1.String442;
        public const string Nil = ServiceModelStringsVersion1.String443;
        public const string Type = ServiceModelStringsVersion1.String444;
        public const string Char = ServiceModelStringsVersion1.String445;
        public const string Boolean = ServiceModelStringsVersion1.String446;
        public const string Byte = ServiceModelStringsVersion1.String447;
        public const string UnsignedByte = ServiceModelStringsVersion1.String448;
        public const string Short = ServiceModelStringsVersion1.String449;
        public const string UnsignedShort = ServiceModelStringsVersion1.String450;
        public const string Int = ServiceModelStringsVersion1.String451;
        public const string UnsignedInt = ServiceModelStringsVersion1.String452;
        public const string Long = ServiceModelStringsVersion1.String453;
        public const string UnsignedLong = ServiceModelStringsVersion1.String454;
        public const string Float = ServiceModelStringsVersion1.String455;
        public const string Double = ServiceModelStringsVersion1.String456;
        public const string Decimal = ServiceModelStringsVersion1.String457;
        public const string DateTime = ServiceModelStringsVersion1.String458;
        public const string String = ServiceModelStringsVersion1.String459;
        public const string Base64Binary = ServiceModelStringsVersion1.String460;
        public const string AnyType = ServiceModelStringsVersion1.String461;
        public const string Duration = ServiceModelStringsVersion1.String462;
        public const string Guid = ServiceModelStringsVersion1.String463;
        public const string AnyURI = ServiceModelStringsVersion1.String464;
        public const string QName = ServiceModelStringsVersion1.String465;
        public const string Time = ServiceModelStringsVersion1.String466;
        public const string Date = ServiceModelStringsVersion1.String467;
        public const string HexBinary = ServiceModelStringsVersion1.String468;
        public const string GYearMonth = ServiceModelStringsVersion1.String469;
        public const string GYear = ServiceModelStringsVersion1.String470;
        public const string GMonthDay = ServiceModelStringsVersion1.String471;
        public const string GDay = ServiceModelStringsVersion1.String472;
        public const string GMonth = ServiceModelStringsVersion1.String473;
        public const string Integer = ServiceModelStringsVersion1.String474;
        public const string PositiveInteger = ServiceModelStringsVersion1.String475;
        public const string NegativeInteger = ServiceModelStringsVersion1.String476;
        public const string NonPositiveInteger = ServiceModelStringsVersion1.String477;
        public const string NonNegativeInteger = ServiceModelStringsVersion1.String478;
        public const string NormalizedString = ServiceModelStringsVersion1.String479;
    }

    internal static class TrustStrings
    {
    }

    internal static class TrustApr2004Strings
    {
        // Main dictionary strings
        public const string CombinedHashLabel = ServiceModelStringsVersion1.String194;
        public const string RequestSecurityTokenResponse = ServiceModelStringsVersion1.String195;
        public const string TokenType = ServiceModelStringsVersion1.String187;
        public const string KeySize = ServiceModelStringsVersion1.String196;
        public const string RequestedTokenReference = ServiceModelStringsVersion1.String197;
        public const string AppliesTo = ServiceModelStringsVersion1.String198;
        public const string Authenticator = ServiceModelStringsVersion1.String199;
        public const string CombinedHash = ServiceModelStringsVersion1.String200;
        public const string BinaryExchange = ServiceModelStringsVersion1.String201;
        public const string Lifetime = ServiceModelStringsVersion1.String202;
        public const string RequestedSecurityToken = ServiceModelStringsVersion1.String203;
        public const string Entropy = ServiceModelStringsVersion1.String204;
        public const string RequestedProofToken = ServiceModelStringsVersion1.String205;
        public const string ComputedKey = ServiceModelStringsVersion1.String206;
        public const string RequestSecurityToken = ServiceModelStringsVersion1.String207;
        public const string RequestType = ServiceModelStringsVersion1.String208;
        public const string RequestSecurityTokenResponseCollection = ServiceModelStringsVersion1.String62;
        public const string Context = ServiceModelStringsVersion1.String209;
        public const string BinarySecret = ServiceModelStringsVersion1.String210;
        public const string Type = ServiceModelStringsVersion1.String59;
        public const string SpnegoValueTypeUri = ServiceModelStringsVersion1.String211;
        public const string TlsnegoValueTypeUri = ServiceModelStringsVersion1.String212;
        public const string Prefix = ServiceModelStringsVersion1.String213;
        public const string Namespace = ServiceModelStringsVersion1.String214;
        public const string RequestSecurityTokenIssuance = ServiceModelStringsVersion1.String215;
        public const string RequestSecurityTokenIssuanceResponse = ServiceModelStringsVersion1.String216;
        public const string RequestTypeIssue = ServiceModelStringsVersion1.String217;
        public const string Psha1ComputedKeyUri = ServiceModelStringsVersion1.String218;
        public const string SymmetricKeyBinarySecret = ServiceModelStringsVersion1.String219;
        public const string NonceBinarySecret = ServiceModelStringsVersion1.String220;
        public const string KeyType = ServiceModelStringsVersion1.String221;
        public const string SymmetricKeyType = ServiceModelStringsVersion1.String222;
        public const string PublicKeyType = ServiceModelStringsVersion1.String223;
        public const string Claims = ServiceModelStringsVersion1.String224;
        public const string InvalidRequestFaultCode = ServiceModelStringsVersion1.String225;
        public const string FailedAuthenticationFaultCode = ServiceModelStringsVersion1.String182;
        public const string RequestFailedFaultCode = ServiceModelStringsVersion1.String226;
        public const string SignWith = ServiceModelStringsVersion1.String227;
        public const string EncryptWith = ServiceModelStringsVersion1.String228;
        public const string EncryptionAlgorithm = ServiceModelStringsVersion1.String229;
        public const string CanonicalizationAlgorithm = ServiceModelStringsVersion1.String230;
        public const string ComputedKeyAlgorithm = ServiceModelStringsVersion1.String231;
        public const string UseKey = ServiceModelStringsVersion1.String232;
    }

    internal static class TrustFeb2005Strings
    {
        // Main dictionary strings
        public const string RequestSecurityTokenResponseCollection = ServiceModelStringsVersion1.String62;
        public const string Namespace = ServiceModelStringsVersion1.String63;
        public const string BinarySecretClauseType = ServiceModelStringsVersion1.String64;
        public const string CombinedHashLabel = ServiceModelStringsVersion1.String194;
        public const string RequestSecurityTokenResponse = ServiceModelStringsVersion1.String195;
        public const string TokenType = ServiceModelStringsVersion1.String187;
        public const string KeySize = ServiceModelStringsVersion1.String196;
        public const string RequestedTokenReference = ServiceModelStringsVersion1.String197;
        public const string AppliesTo = ServiceModelStringsVersion1.String198;
        public const string Authenticator = ServiceModelStringsVersion1.String199;
        public const string CombinedHash = ServiceModelStringsVersion1.String200;
        public const string BinaryExchange = ServiceModelStringsVersion1.String201;
        public const string Lifetime = ServiceModelStringsVersion1.String202;
        public const string RequestedSecurityToken = ServiceModelStringsVersion1.String203;
        public const string Entropy = ServiceModelStringsVersion1.String204;
        public const string RequestedProofToken = ServiceModelStringsVersion1.String205;
        public const string ComputedKey = ServiceModelStringsVersion1.String206;
        public const string RequestSecurityToken = ServiceModelStringsVersion1.String207;
        public const string RequestType = ServiceModelStringsVersion1.String208;
        public const string Context = ServiceModelStringsVersion1.String209;
        public const string BinarySecret = ServiceModelStringsVersion1.String210;
        public const string Type = ServiceModelStringsVersion1.String59;
        public const string SpnegoValueTypeUri = ServiceModelStringsVersion1.String233;
        public const string TlsnegoValueTypeUri = ServiceModelStringsVersion1.String234;
        public const string Prefix = ServiceModelStringsVersion1.String235;
        public const string RequestSecurityTokenIssuance = ServiceModelStringsVersion1.String236;
        public const string RequestSecurityTokenIssuanceResponse = ServiceModelStringsVersion1.String237;
        public const string RequestTypeIssue = ServiceModelStringsVersion1.String238;
        public const string SymmetricKeyBinarySecret = ServiceModelStringsVersion1.String239;
        public const string Psha1ComputedKeyUri = ServiceModelStringsVersion1.String240;
        public const string NonceBinarySecret = ServiceModelStringsVersion1.String241;
        public const string RenewTarget = ServiceModelStringsVersion1.String242;
        public const string CloseTarget = ServiceModelStringsVersion1.String243;
        public const string RequestedTokenClosed = ServiceModelStringsVersion1.String244;
        public const string RequestedAttachedReference = ServiceModelStringsVersion1.String245;
        public const string RequestedUnattachedReference = ServiceModelStringsVersion1.String246;
        public const string IssuedTokensHeader = ServiceModelStringsVersion1.String247;
        public const string RequestTypeRenew = ServiceModelStringsVersion1.String248;
        public const string RequestTypeClose = ServiceModelStringsVersion1.String249;
        public const string KeyType = ServiceModelStringsVersion1.String221;
        public const string SymmetricKeyType = ServiceModelStringsVersion1.String239;
        public const string PublicKeyType = ServiceModelStringsVersion1.String250;
        public const string Claims = ServiceModelStringsVersion1.String224;
        public const string InvalidRequestFaultCode = ServiceModelStringsVersion1.String225;
        public const string FailedAuthenticationFaultCode = ServiceModelStringsVersion1.String182;
        public const string UseKey = ServiceModelStringsVersion1.String232;
        public const string SignWith = ServiceModelStringsVersion1.String227;
        public const string EncryptWith = ServiceModelStringsVersion1.String228;
        public const string EncryptionAlgorithm = ServiceModelStringsVersion1.String229;
        public const string CanonicalizationAlgorithm = ServiceModelStringsVersion1.String230;
        public const string ComputedKeyAlgorithm = ServiceModelStringsVersion1.String231;
    }

    internal static class UtilityStrings
    {
        // Main dictionary strings
        public const string IdAttribute = ServiceModelStringsVersion1.String14;
        public const string Namespace = ServiceModelStringsVersion1.String51;
        public const string Timestamp = ServiceModelStringsVersion1.String53;
        public const string CreatedElement = ServiceModelStringsVersion1.String54;
        public const string ExpiresElement = ServiceModelStringsVersion1.String55;
        public const string Prefix = ServiceModelStringsVersion1.String305;
        public const string UniqueEndpointHeaderName = ServiceModelStringsVersion1.String306;
        public const string UniqueEndpointHeaderNamespace = ServiceModelStringsVersion1.String307;
    }

    internal static class WsrmFeb2005Strings
    {
        // Main dictionary strings
        public const string Identifier = ServiceModelStringsVersion1.String15;
        public const string Namespace = ServiceModelStringsVersion1.String16;
        public const string SequenceAcknowledgement = ServiceModelStringsVersion1.String23;
        public const string AcknowledgementRange = ServiceModelStringsVersion1.String24;
        public const string Upper = ServiceModelStringsVersion1.String25;
        public const string Lower = ServiceModelStringsVersion1.String26;
        public const string BufferRemaining = ServiceModelStringsVersion1.String27;
        public const string NETNamespace = ServiceModelStringsVersion1.String28;
        public const string SequenceAcknowledgementAction = ServiceModelStringsVersion1.String29;
        public const string Sequence = ServiceModelStringsVersion1.String31;
        public const string MessageNumber = ServiceModelStringsVersion1.String32;
        public const string AckRequested = ServiceModelStringsVersion1.String328;
        public const string AckRequestedAction = ServiceModelStringsVersion1.String329;
        public const string AcksTo = ServiceModelStringsVersion1.String330;
        public const string Accept = ServiceModelStringsVersion1.String331;
        public const string CreateSequence = ServiceModelStringsVersion1.String332;
        public const string CreateSequenceAction = ServiceModelStringsVersion1.String333;
        public const string CreateSequenceRefused = ServiceModelStringsVersion1.String334;
        public const string CreateSequenceResponse = ServiceModelStringsVersion1.String335;
        public const string CreateSequenceResponseAction = ServiceModelStringsVersion1.String336;
        public const string Expires = ServiceModelStringsVersion1.String55;
        public const string FaultCode = ServiceModelStringsVersion1.String337;
        public const string InvalidAcknowledgement = ServiceModelStringsVersion1.String338;
        public const string LastMessage = ServiceModelStringsVersion1.String339;
        public const string LastMessageAction = ServiceModelStringsVersion1.String340;
        public const string LastMessageNumberExceeded = ServiceModelStringsVersion1.String341;
        public const string MessageNumberRollover = ServiceModelStringsVersion1.String342;
        public const string Nack = ServiceModelStringsVersion1.String343;
        public const string NETPrefix = ServiceModelStringsVersion1.String344;
        public const string Offer = ServiceModelStringsVersion1.String345;
        public const string Prefix = ServiceModelStringsVersion1.String346;
        public const string SequenceFault = ServiceModelStringsVersion1.String347;
        public const string SequenceTerminated = ServiceModelStringsVersion1.String348;
        public const string TerminateSequence = ServiceModelStringsVersion1.String349;
        public const string TerminateSequenceAction = ServiceModelStringsVersion1.String350;
        public const string UnknownSequence = ServiceModelStringsVersion1.String351;
        public const string ConnectionLimitReached = ServiceModelStringsVersion1.String480;
    }

    internal static class XmlEncryptionStrings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String37;
        public const string DataReference = ServiceModelStringsVersion1.String46;
        public const string EncryptedData = ServiceModelStringsVersion1.String47;
        public const string EncryptionMethod = ServiceModelStringsVersion1.String48;
        public const string CipherData = ServiceModelStringsVersion1.String49;
        public const string CipherValue = ServiceModelStringsVersion1.String50;
        public const string ReferenceList = ServiceModelStringsVersion1.String57;
        public const string Encoding = ServiceModelStringsVersion1.String308;
        public const string MimeType = ServiceModelStringsVersion1.String309;
        public const string Type = ServiceModelStringsVersion1.String59;
        public const string Id = ServiceModelStringsVersion1.String14;
        public const string CarriedKeyName = ServiceModelStringsVersion1.String310;
        public const string Recipient = ServiceModelStringsVersion1.String311;
        public const string EncryptedKey = ServiceModelStringsVersion1.String312;
        public const string URI = ServiceModelStringsVersion1.String11;
        public const string KeyReference = ServiceModelStringsVersion1.String313;
        public const string Prefix = ServiceModelStringsVersion1.String314;
        public const string ElementType = ServiceModelStringsVersion1.String315;
        public const string ContentType = ServiceModelStringsVersion1.String316;
        public const string AlgorithmAttribute = ServiceModelStringsVersion1.String8;
    }

    internal static class XmlSignatureStrings
    {
        // Main dictionary strings
        public const string Algorithm = ServiceModelStringsVersion1.String8;
        public const string URI = ServiceModelStringsVersion1.String11;
        public const string Reference = ServiceModelStringsVersion1.String12;
        public const string Transforms = ServiceModelStringsVersion1.String17;
        public const string Transform = ServiceModelStringsVersion1.String18;
        public const string DigestMethod = ServiceModelStringsVersion1.String19;
        public const string DigestValue = ServiceModelStringsVersion1.String20;
        public const string Namespace = ServiceModelStringsVersion1.String33;
        public const string EnvelopedSignature = ServiceModelStringsVersion1.String34;
        public const string KeyInfo = ServiceModelStringsVersion1.String35;
        public const string Signature = ServiceModelStringsVersion1.String41;
        public const string SignedInfo = ServiceModelStringsVersion1.String42;
        public const string CanonicalizationMethod = ServiceModelStringsVersion1.String43;
        public const string SignatureMethod = ServiceModelStringsVersion1.String44;
        public const string SignatureValue = ServiceModelStringsVersion1.String45;
        public const string KeyName = ServiceModelStringsVersion1.String317;
        public const string Type = ServiceModelStringsVersion1.String59;
        public const string MgmtData = ServiceModelStringsVersion1.String318;
        public const string Prefix = ServiceModelStringsVersion1.String81;
        public const string KeyValue = ServiceModelStringsVersion1.String319;
        public const string RsaKeyValue = ServiceModelStringsVersion1.String320;
        public const string Modulus = ServiceModelStringsVersion1.String321;
        public const string Exponent = ServiceModelStringsVersion1.String322;
        public const string X509Data = ServiceModelStringsVersion1.String323;
        public const string X509IssuerSerial = ServiceModelStringsVersion1.String324;
        public const string X509IssuerName = ServiceModelStringsVersion1.String325;
        public const string X509SerialNumber = ServiceModelStringsVersion1.String326;
        public const string X509Certificate = ServiceModelStringsVersion1.String327;
        // String constants
        public const string SecurityJan2004Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        public const string SecurityJan2004Prefix = "o";
        public const string X509Ski = "X509SKI";
        public const string TransformationParameters = "TransformationParameters";
    }
}