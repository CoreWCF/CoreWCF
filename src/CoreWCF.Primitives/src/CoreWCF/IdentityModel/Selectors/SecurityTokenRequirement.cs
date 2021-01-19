using CoreWCF.IdentityModel.Tokens;
using CoreWCF;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    public class SecurityTokenRequirement
    {
        const string Namespace = "http://schemas.microsoft.com/ws/2006/05/identitymodel/securitytokenrequirement";
        const string tokenTypeProperty = Namespace + "/TokenType";
        const string keyUsageProperty = Namespace + "/KeyUsage";
        const string keyTypeProperty = Namespace + "/KeyType";
        const string keySizeProperty = Namespace + "/KeySize";
        const string requireCryptographicTokenProperty = Namespace + "/RequireCryptographicToken";
        const string peerAuthenticationMode = Namespace + "/PeerAuthenticationMode";
        const string isOptionalTokenProperty = Namespace + "/IsOptionalTokenProperty";

        const bool defaultRequireCryptographicToken = false;
        const SecurityKeyUsage defaultKeyUsage = SecurityKeyUsage.Signature;
        const SecurityKeyType defaultKeyType = SecurityKeyType.SymmetricKey;
        const int defaultKeySize = 0;
        const bool defaultIsOptionalToken = false;

        Dictionary<string, object> properties;

        public SecurityTokenRequirement()
        {
            properties = new Dictionary<string, object>();
            Initialize();
        }

        static public string TokenTypeProperty { get { return tokenTypeProperty; } }
        static public string KeyUsageProperty { get { return keyUsageProperty; } }
        static public string KeyTypeProperty { get { return keyTypeProperty; } }
        static public string KeySizeProperty { get { return keySizeProperty; } }
        static public string RequireCryptographicTokenProperty { get { return requireCryptographicTokenProperty; } }
        static public string PeerAuthenticationMode { get { return peerAuthenticationMode; } }
        static public string IsOptionalTokenProperty { get { return isOptionalTokenProperty; } }

        public string TokenType
        {
            get
            {
                string result;
                return (TryGetProperty<string>(TokenTypeProperty, out result)) ? result : null;
            }
            set
            {
                properties[TokenTypeProperty] = value;
            }
        }

        internal bool IsOptionalToken
        {
            get
            {
                bool result;
                return (TryGetProperty<bool>(IsOptionalTokenProperty, out result)) ? result : defaultIsOptionalToken;
            }
            set
            {
                properties[IsOptionalTokenProperty] = value;
            }
        }

        public bool RequireCryptographicToken
        {
            get
            {
                bool result;
                return (TryGetProperty<bool>(RequireCryptographicTokenProperty, out result)) ? result : defaultRequireCryptographicToken;
            }
            set
            {
                properties[RequireCryptographicTokenProperty] = (object)value;
            }
        }

        internal SecurityKeyUsage KeyUsage
        {
            get
            {
                SecurityKeyUsage result;
                return (TryGetProperty<SecurityKeyUsage>(KeyUsageProperty, out result)) ? result : defaultKeyUsage;
            }
            set
            {
                SecurityKeyUsageHelper.Validate(value);
                properties[KeyUsageProperty] = (object)value;
            }
        }

        internal SecurityKeyType KeyType
        {
            get
            {
                SecurityKeyType result;
                return (TryGetProperty<SecurityKeyType>(KeyTypeProperty, out result)) ? result : defaultKeyType;
            }
            set
            {
                SecurityKeyTypeHelper.Validate(value);
                properties[KeyTypeProperty] = (object)value;
            }
        }

        public int KeySize
        {
            get
            {
                int result;
                return (TryGetProperty<int>(KeySizeProperty, out result)) ? result : defaultKeySize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.ValueMustBeNonNegative));
                }
                Properties[KeySizeProperty] = value;
            }
        }

        public IDictionary<string, object> Properties
        {
            get
            {
                return properties;
            }
        }

        void Initialize()
        {
            KeyType = defaultKeyType;
            KeyUsage = defaultKeyUsage;
            RequireCryptographicToken = defaultRequireCryptographicToken;
            KeySize = defaultKeySize;
            IsOptionalToken = defaultIsOptionalToken;
        }

        public TValue GetProperty<TValue>(string propertyName)
        {
            TValue result;
            if (!TryGetProperty<TValue>(propertyName, out result))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SecurityTokenRequirementDoesNotContainProperty, propertyName)));
            }
            return result;
        }

        public bool TryGetProperty<TValue>(string propertyName, out TValue result)
        {
            object dictionaryValue;
            if (!Properties.TryGetValue(propertyName, out dictionaryValue))
            {
                result = default(TValue);
                return false;
            }
            if (dictionaryValue != null && !typeof(TValue).IsAssignableFrom(dictionaryValue.GetType()))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SecurityTokenRequirementHasInvalidTypeForProperty, propertyName, dictionaryValue.GetType(), typeof(TValue))));
            }
            result = (TValue)dictionaryValue;
            return true;
        }

    }
}
