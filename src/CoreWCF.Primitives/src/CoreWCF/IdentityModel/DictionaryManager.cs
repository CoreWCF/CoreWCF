using System.Xml;
using System;

namespace CoreWCF.IdentityModel
{
    internal class DictionaryManager
    {
        private SamlDictionary _samlDictionary;
        private XmlSignatureDictionary _sigantureDictionary;
        private UtilityDictionary _utilityDictionary;
        private ExclusiveC14NDictionary _exclusiveC14NDictionary;
        private SecurityAlgorithmDec2005Dictionary _securityAlgorithmDec2005Dictionary;
        private SecurityAlgorithmDictionary _securityAlgorithmDictionary;
        private SecurityJan2004Dictionary _securityJan2004Dictionary;
        private SecurityXXX2005Dictionary _securityJanXXX2005Dictionary;
        private SecureConversationDec2005Dictionary _secureConversationDec2005Dictionary;
        private SecureConversationFeb2005Dictionary _secureConversationFeb2005Dictionary;
        private TrustFeb2005Dictionary _trustFeb2005Dictionary;
        private TrustDec2005Dictionary _trustDec2005Dictionary;
        private XmlEncryptionDictionary _xmlEncryptionDictionary;
        private IXmlDictionary _parentDictionary;

        public DictionaryManager()
        {
            _samlDictionary = CoreWCF.XD.SamlDictionary;
            _sigantureDictionary = CoreWCF.XD.XmlSignatureDictionary;
            _utilityDictionary = CoreWCF.XD.UtilityDictionary;
            _exclusiveC14NDictionary = CoreWCF.XD.ExclusiveC14NDictionary;
            _securityAlgorithmDictionary = CoreWCF.XD.SecurityAlgorithmDictionary;
            _parentDictionary = CoreWCF.XD.Dictionary;
            _securityJan2004Dictionary = CoreWCF.XD.SecurityJan2004Dictionary;
            _securityJanXXX2005Dictionary = CoreWCF.XD.SecurityXXX2005Dictionary;
            _secureConversationFeb2005Dictionary = CoreWCF.XD.SecureConversationFeb2005Dictionary;
            _trustFeb2005Dictionary = CoreWCF.XD.TrustFeb2005Dictionary;
            _xmlEncryptionDictionary = CoreWCF.XD.XmlEncryptionDictionary;

            // These 3 are factored into a seperate dictionary in ServiceModel under DXD. 
            _secureConversationDec2005Dictionary = DXD.SecureConversationDec2005Dictionary;
            _securityAlgorithmDec2005Dictionary = DXD.SecurityAlgorithmDec2005Dictionary;
            _trustDec2005Dictionary = DXD.TrustDec2005Dictionary;
        }

        
        public DictionaryManager(ServiceModelDictionary parentDictionary)
        {
            _samlDictionary = new SamlDictionary(parentDictionary);
            _sigantureDictionary = new XmlSignatureDictionary(parentDictionary);
            _utilityDictionary = new UtilityDictionary(parentDictionary);
            _exclusiveC14NDictionary = new ExclusiveC14NDictionary(parentDictionary);
            _securityAlgorithmDictionary = new SecurityAlgorithmDictionary(parentDictionary);
            _securityJan2004Dictionary = new SecurityJan2004Dictionary(parentDictionary);
            _securityJanXXX2005Dictionary = new SecurityXXX2005Dictionary(parentDictionary);
            _secureConversationFeb2005Dictionary = new SecureConversationFeb2005Dictionary(parentDictionary);
            _trustFeb2005Dictionary = new TrustFeb2005Dictionary(parentDictionary);
            _xmlEncryptionDictionary = new XmlEncryptionDictionary(parentDictionary);
            _parentDictionary = parentDictionary;

            // These 3 are factored into a seperate dictionary in ServiceModel under DXD. 
            // ServiceModel should set these seperately using the property setters.
            _secureConversationDec2005Dictionary = DXD.SecureConversationDec2005Dictionary;
            _securityAlgorithmDec2005Dictionary = DXD.SecurityAlgorithmDec2005Dictionary;
            _trustDec2005Dictionary = DXD.TrustDec2005Dictionary;
        }

        public SamlDictionary SamlDictionary
        {
            get { return _samlDictionary; }
            set { _samlDictionary = value; }
        }

        public XmlSignatureDictionary XmlSignatureDictionary
        {
            get { return _sigantureDictionary; }
            set { _sigantureDictionary = value; }
        }

        public UtilityDictionary UtilityDictionary
        {
            get { return _utilityDictionary; }
            set { _utilityDictionary = value; }
        }

        public ExclusiveC14NDictionary ExclusiveC14NDictionary
        {
            get { return _exclusiveC14NDictionary; }
            set { _exclusiveC14NDictionary = value; }
        }

        public SecurityAlgorithmDec2005Dictionary SecurityAlgorithmDec2005Dictionary
        {
            get { return _securityAlgorithmDec2005Dictionary; }
            set { _securityAlgorithmDec2005Dictionary = value; }
        }

        public SecurityAlgorithmDictionary SecurityAlgorithmDictionary
        {
            get { return _securityAlgorithmDictionary; }
            set { _securityAlgorithmDictionary = value; }
        }

        public SecurityJan2004Dictionary SecurityJan2004Dictionary
        {
            get { return _securityJan2004Dictionary; }
            set { _securityJan2004Dictionary = value; }
        }

        public SecurityXXX2005Dictionary SecurityJanXXX2005Dictionary
        {
            get { return _securityJanXXX2005Dictionary; }
            set { _securityJanXXX2005Dictionary = value; }
        }

        public SecureConversationDec2005Dictionary SecureConversationDec2005Dictionary
        {
            get { return _secureConversationDec2005Dictionary; }
            set { _secureConversationDec2005Dictionary = value; }
        }

        public SecureConversationFeb2005Dictionary SecureConversationFeb2005Dictionary
        {
            get { return _secureConversationFeb2005Dictionary; }
            set { _secureConversationFeb2005Dictionary = value; }
        }

        public TrustDec2005Dictionary TrustDec2005Dictionary
        {
            get { return _trustDec2005Dictionary; }
            set { _trustDec2005Dictionary = value; }
        }

        public TrustFeb2005Dictionary TrustFeb2005Dictionary
        {
            get { return _trustFeb2005Dictionary; }
            set { _trustFeb2005Dictionary = value; }
        }

        public XmlEncryptionDictionary XmlEncryptionDictionary
        {
            get { return _xmlEncryptionDictionary; }
            set { _xmlEncryptionDictionary = value; }
        }

        public IXmlDictionary ParentDictionary
        {
            get { return _parentDictionary; }
            set { _parentDictionary = value; }
        }
    }
}
