// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Xml;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;

namespace CoreWCF.Security.Tokens
{
    internal struct SecurityContextCookieSerializer
    {
        private const int SupportedPersistanceVersion = 1;
        private readonly SecurityStateEncoder _securityStateEncoder;
        private readonly IList<Type> _knownTypes;

        public SecurityContextCookieSerializer(SecurityStateEncoder securityStateEncoder, IList<Type> knownTypes)
        {
            _securityStateEncoder = securityStateEncoder ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityStateEncoder));
            _knownTypes = knownTypes ?? new List<Type>();
        }

        private SecurityContextSecurityToken DeserializeContext(byte[] serializedContext, byte[] cookieBlob, string id, XmlDictionaryReaderQuotas quotas)
        {
            SctClaimDictionary dictionary = SctClaimDictionary.Instance;
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(serializedContext, 0, serializedContext.Length, dictionary, quotas, null, null);
            int cookieVersion = -1;
            UniqueId cookieContextId = null;
            DateTime effectiveTime = SecurityUtils.MinUtcDateTime;
            DateTime expiryTime = SecurityUtils.MaxUtcDateTime;
            byte[] key = null;
            string localId = null;
            UniqueId keyGeneration = null;
            DateTime keyEffectiveTime = SecurityUtils.MinUtcDateTime;
            DateTime keyExpirationTime = SecurityUtils.MaxUtcDateTime;
            List<ClaimSet> claimSets = null;
            IList<IIdentity> identities = null;
            bool isCookie = true;

            reader.ReadFullStartElement(dictionary.SecurityContextSecurityToken, dictionary.EmptyString);

            while (reader.IsStartElement())
            {
                if (reader.IsStartElement(dictionary.Version, dictionary.EmptyString))
                {
                    cookieVersion = reader.ReadElementContentAsInt();
                }
                else if (reader.IsStartElement(dictionary.ContextId, dictionary.EmptyString))
                {
                    cookieContextId = reader.ReadElementContentAsUniqueId();
                }
                else if (reader.IsStartElement(dictionary.Id, dictionary.EmptyString))
                {
                    localId = reader.ReadElementContentAsString();
                }
                else if (reader.IsStartElement(dictionary.EffectiveTime, dictionary.EmptyString))
                {
                    effectiveTime = new DateTime(XmlHelper.ReadElementContentAsInt64(reader), DateTimeKind.Utc);
                }
                else if (reader.IsStartElement(dictionary.ExpiryTime, dictionary.EmptyString))
                {
                    expiryTime = new DateTime(XmlHelper.ReadElementContentAsInt64(reader), DateTimeKind.Utc);
                }
                else if (reader.IsStartElement(dictionary.Key, dictionary.EmptyString))
                {
                    key = reader.ReadElementContentAsBase64();
                }
                else if (reader.IsStartElement(dictionary.KeyGeneration, dictionary.EmptyString))
                {
                    keyGeneration = reader.ReadElementContentAsUniqueId();
                }
                else if (reader.IsStartElement(dictionary.KeyEffectiveTime, dictionary.EmptyString))
                {
                    keyEffectiveTime = new DateTime(XmlHelper.ReadElementContentAsInt64(reader), DateTimeKind.Utc);
                }
                else if (reader.IsStartElement(dictionary.KeyExpiryTime, dictionary.EmptyString))
                {
                    keyExpirationTime = new DateTime(XmlHelper.ReadElementContentAsInt64(reader), DateTimeKind.Utc);
                }
                else if (reader.IsStartElement(dictionary.Identities, dictionary.EmptyString))
                {
                    identities = SctClaimSerializer.DeserializeIdentities(reader, dictionary, DataContractSerializerDefaults.CreateSerializer(typeof(IIdentity), _knownTypes, int.MaxValue));
                }
                else if (reader.IsStartElement(dictionary.ClaimSets, dictionary.EmptyString))
                {
                    reader.ReadStartElement();

                    DataContractSerializer claimSetSerializer = DataContractSerializerDefaults.CreateSerializer(typeof(ClaimSet), _knownTypes, int.MaxValue);
                    DataContractSerializer claimSerializer = DataContractSerializerDefaults.CreateSerializer(typeof(Claim), _knownTypes, int.MaxValue);
                    claimSets = new List<ClaimSet>(1);
                    while (reader.IsStartElement())
                    {
                        claimSets.Add(SctClaimSerializer.DeserializeClaimSet(reader, dictionary, claimSetSerializer, claimSerializer));
                    }

                    reader.ReadEndElement();
                }
                else if (reader.IsStartElement(dictionary.IsCookieMode, dictionary.EmptyString))
                {
                    isCookie = reader.ReadElementString() == "1" ? true : false;
                }
                else
                {
                    OnInvalidCookieFailure(SR.Format(SR.SctCookieXmlParseError));
                }
            }
            reader.ReadEndElement();
            if (cookieVersion != SupportedPersistanceVersion)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SerializedTokenVersionUnsupported, cookieVersion)));
            }
            if (cookieContextId == null)
            {
                OnInvalidCookieFailure(SR.Format(SR.SctCookieValueMissingOrIncorrect, nameof(cookieContextId)));
            }
            if (key == null || key.Length == 0)
            {
                OnInvalidCookieFailure(SR.Format(SR.SctCookieValueMissingOrIncorrect, nameof(key)));
            }
            if (localId != id)
            {
                OnInvalidCookieFailure(SR.Format(SR.SctCookieValueMissingOrIncorrect, nameof(id)));
            }
            List<IAuthorizationPolicy> authorizationPolicies;
            if (claimSets != null)
            {
                authorizationPolicies = new List<IAuthorizationPolicy>(1)
                {
                    new SctUnconditionalPolicy(identities, claimSets, expiryTime)
                };
            }
            else
            {
                authorizationPolicies = null;
            }
            return new SecurityContextSecurityToken(cookieContextId, localId, key, effectiveTime, expiryTime,
                authorizationPolicies?.AsReadOnly(), isCookie, cookieBlob, keyGeneration, keyEffectiveTime, keyExpirationTime);
        }

        public byte[] CreateCookieFromSecurityContext(UniqueId contextId, string id, byte[] key, DateTime tokenEffectiveTime,
            DateTime tokenExpirationTime, UniqueId keyGeneration, DateTime keyEffectiveTime, DateTime keyExpirationTime,
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (contextId == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contextId));
            }

            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            }

            MemoryStream stream = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(stream, SctClaimDictionary.Instance, null);

            SctClaimDictionary dictionary = SctClaimDictionary.Instance;
            writer.WriteStartElement(dictionary.SecurityContextSecurityToken, dictionary.EmptyString);
            writer.WriteStartElement(dictionary.Version, dictionary.EmptyString);
            writer.WriteValue(SupportedPersistanceVersion);
            writer.WriteEndElement();
            if (id != null)
            {
                writer.WriteElementString(dictionary.Id, dictionary.EmptyString, id);
            }

            XmlHelper.WriteElementStringAsUniqueId(writer, dictionary.ContextId, dictionary.EmptyString, contextId);

            writer.WriteStartElement(dictionary.Key, dictionary.EmptyString);
            writer.WriteBase64(key, 0, key.Length);
            writer.WriteEndElement();

            if (keyGeneration != null)
            {
                XmlHelper.WriteElementStringAsUniqueId(writer, dictionary.KeyGeneration, dictionary.EmptyString, keyGeneration);
            }

            XmlHelper.WriteElementContentAsInt64(writer, dictionary.EffectiveTime, dictionary.EmptyString, tokenEffectiveTime.ToUniversalTime().Ticks);
            XmlHelper.WriteElementContentAsInt64(writer, dictionary.ExpiryTime, dictionary.EmptyString, tokenExpirationTime.ToUniversalTime().Ticks);
            XmlHelper.WriteElementContentAsInt64(writer, dictionary.KeyEffectiveTime, dictionary.EmptyString, keyEffectiveTime.ToUniversalTime().Ticks);
            XmlHelper.WriteElementContentAsInt64(writer, dictionary.KeyExpiryTime, dictionary.EmptyString, keyExpirationTime.ToUniversalTime().Ticks);

            AuthorizationContext authContext = null;
            if (authorizationPolicies != null)
            {
                authContext = AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies);
            }

            if (authContext != null && authContext.ClaimSets.Count != 0)
            {
                DataContractSerializer identitySerializer = DataContractSerializerDefaults.CreateSerializer(typeof(IIdentity), _knownTypes, int.MaxValue);
                DataContractSerializer claimSetSerializer = DataContractSerializerDefaults.CreateSerializer(typeof(ClaimSet), _knownTypes, int.MaxValue);
                DataContractSerializer claimSerializer = DataContractSerializerDefaults.CreateSerializer(typeof(Claim), _knownTypes, int.MaxValue);
                SctClaimSerializer.SerializeIdentities(authContext, dictionary, writer, identitySerializer);

                writer.WriteStartElement(dictionary.ClaimSets, dictionary.EmptyString);
                for (int i = 0; i < authContext.ClaimSets.Count; i++)
                {
                    SctClaimSerializer.SerializeClaimSet(authContext.ClaimSets[i], dictionary, writer, claimSetSerializer, claimSerializer);
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.Flush();

            byte[] serializedContext = stream.ToArray();
            return _securityStateEncoder.EncodeSecurityState(serializedContext);
        }

        public SecurityContextSecurityToken CreateSecurityContextFromCookie(byte[] encodedCookie, UniqueId contextId, UniqueId generation, string id, XmlDictionaryReaderQuotas quotas)
        {
            byte[] cookie = null;

            try
            {
                cookie = _securityStateEncoder.DecodeSecurityState(encodedCookie);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                OnInvalidCookieFailure(SR.Format(SR.SctCookieBlobDecodeFailure), e);
            }
            SecurityContextSecurityToken sct = DeserializeContext(cookie, encodedCookie, id, quotas);
            if (sct.ContextId != contextId)
            {
                OnInvalidCookieFailure(SR.Format(SR.SctCookieValueMissingOrIncorrect, nameof(contextId)));
            }
            if (sct.KeyGeneration != generation)
            {
                OnInvalidCookieFailure(SR.Format(SR.SctCookieValueMissingOrIncorrect, nameof(sct.KeyGeneration)));
            }

            return sct;
        }

        internal static void OnInvalidCookieFailure(string reason)
        {
            OnInvalidCookieFailure(reason, null);
        }

        internal static void OnInvalidCookieFailure(string reason, Exception e)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.InvalidSecurityContextCookie, reason), e));
        }

        internal class SctUnconditionalPolicy : IAuthorizationPolicy
        {
            private readonly SecurityUniqueId _id = SecurityUniqueId.Create();
            private readonly IList<IIdentity> _identities;
            private readonly IList<ClaimSet> _claimSets;
            private readonly DateTime _expirationTime;

            public SctUnconditionalPolicy(IList<IIdentity> identities, IList<ClaimSet> claimSets, DateTime expirationTime)
            {
                _identities = identities;
                _claimSets = claimSets;
                _expirationTime = expirationTime;
            }

            public string Id
            {
                get { return _id.Value; }
            }

            public ClaimSet Issuer
            {
                get { return ClaimSet.System; }
            }

            public bool Evaluate(EvaluationContext evaluationContext, ref object state)
            {
                for (int i = 0; i < _claimSets.Count; ++i)
                {
                    evaluationContext.AddClaimSet(this, _claimSets[i]);
                }

                if (_identities != null)
                {
                    if (!evaluationContext.Properties.TryGetValue(SecurityUtils.Identities, out object obj))
                    {
                        evaluationContext.Properties.Add(SecurityUtils.Identities, _identities);
                    }
                    else
                    {
                        // null if other overrides the property with something else
                        if (obj is List<IIdentity> dstIdentities)
                        {
                            dstIdentities.AddRange(_identities);
                        }
                    }
                }
                evaluationContext.RecordExpirationTime(_expirationTime);
                return true;
            }
        }
    }
}
