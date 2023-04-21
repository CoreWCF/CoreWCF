﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    public class SecurityTokenRequirement
    {
        private const string Namespace = "http://schemas.microsoft.com/ws/2006/05/identitymodel/securitytokenrequirement";
        private const string tokenTypeProperty = Namespace + "/TokenType";
        private const string keyUsageProperty = Namespace + "/KeyUsage";
        private const string keyTypeProperty = Namespace + "/KeyType";
        private const string keySizeProperty = Namespace + "/KeySize";
        private const string requireCryptographicTokenProperty = Namespace + "/RequireCryptographicToken";
        private const string peerAuthenticationMode = Namespace + "/PeerAuthenticationMode";
        private const string isOptionalTokenProperty = Namespace + "/IsOptionalTokenProperty";
        private const bool defaultRequireCryptographicToken = false;
        private const SecurityKeyUsage defaultKeyUsage = SecurityKeyUsage.Signature;
        private const SecurityKeyType defaultKeyType = SecurityKeyType.SymmetricKey;
        private const int defaultKeySize = 0;
        private const bool defaultIsOptionalToken = false;
        private readonly Dictionary<string, object> _properties;

        public SecurityTokenRequirement()
        {
            _properties = new Dictionary<string, object>();
            Initialize();
        }

        public static string TokenTypeProperty { get { return tokenTypeProperty; } }
        public static string KeyUsageProperty { get { return keyUsageProperty; } }
        public static string KeyTypeProperty { get { return keyTypeProperty; } }
        public static string KeySizeProperty { get { return keySizeProperty; } }
        public static string RequireCryptographicTokenProperty { get { return requireCryptographicTokenProperty; } }
        public static string PeerAuthenticationMode { get { return peerAuthenticationMode; } }
        public static string IsOptionalTokenProperty { get { return isOptionalTokenProperty; } }

        public string TokenType
        {
            get
            {
                return (TryGetProperty<string>(TokenTypeProperty, out string result)) ? result : null;
            }
            set
            {
                _properties[TokenTypeProperty] = value;
            }
        }

        internal bool IsOptionalToken
        {
            get
            {
                return (TryGetProperty<bool>(IsOptionalTokenProperty, out bool result)) ? result : defaultIsOptionalToken;
            }
            set
            {
                _properties[IsOptionalTokenProperty] = value;
            }
        }

        public bool RequireCryptographicToken
        {
            get
            {
                return (TryGetProperty<bool>(RequireCryptographicTokenProperty, out bool result)) ? result : defaultRequireCryptographicToken;
            }
            set
            {
                _properties[RequireCryptographicTokenProperty] = (object)value;
            }
        }

        public SecurityKeyUsage KeyUsage
        {
            get
            {
                return (TryGetProperty<SecurityKeyUsage>(KeyUsageProperty, out SecurityKeyUsage result)) ? result : defaultKeyUsage;
            }
            set
            {
                SecurityKeyUsageHelper.Validate(value);
                _properties[KeyUsageProperty] = (object)value;
            }
        }

        internal SecurityKeyType KeyType
        {
            get
            {
                return (TryGetProperty<SecurityKeyType>(KeyTypeProperty, out SecurityKeyType result)) ? result : defaultKeyType;
            }
            set
            {
                SecurityKeyTypeHelper.Validate(value);
                _properties[KeyTypeProperty] = (object)value;
            }
        }

        public int KeySize
        {
            get
            {
                return (TryGetProperty<int>(KeySizeProperty, out int result)) ? result : defaultKeySize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SRCommon.ValueMustBeNonNegative));
                }
                Properties[KeySizeProperty] = value;
            }
        }

        public IDictionary<string, object> Properties
        {
            get
            {
                return _properties;
            }
        }

        private void Initialize()
        {
            KeyType = defaultKeyType;
            KeyUsage = defaultKeyUsage;
            RequireCryptographicToken = defaultRequireCryptographicToken;
            KeySize = defaultKeySize;
            IsOptionalToken = defaultIsOptionalToken;
        }

        public TValue GetProperty<TValue>(string propertyName)
        {
            if (!TryGetProperty<TValue>(propertyName, out TValue result))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SecurityTokenRequirementDoesNotContainProperty, propertyName)));
            }
            return result;
        }

        public bool TryGetProperty<TValue>(string propertyName, out TValue result)
        {
            if (!Properties.TryGetValue(propertyName, out object dictionaryValue))
            {
                result = default;
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
