// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SecurityStandardsManager
    {
        private static SecurityStandardsManager s_instance;
        private WSSecurityTokenSerializer _wsSecurityTokenSerializer;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public SecurityStandardsManager()
            : this(WSSecurityTokenSerializer.DefaultInstance)
        {
        }

        public SecurityStandardsManager(SecurityTokenSerializer tokenSerializer)
            : this(MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11, tokenSerializer)
        {
        }

        public SecurityStandardsManager(MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer tokenSerializer)
        {
            MessageSecurityVersion = messageSecurityVersion ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(messageSecurityVersion)));
            SecurityTokenSerializer = tokenSerializer ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenSerializer));
            if (messageSecurityVersion.SecureConversationVersion == SecureConversationVersion.WSSecureConversation13)
            {
                SecureConversationDriver = new WSSecureConversationDec2005.DriverDec2005();
            }
            else
            {
                SecureConversationDriver = new WSSecureConversationFeb2005.DriverFeb2005();
            }

            if (SecurityVersion == SecurityVersion.WSSecurity10 || SecurityVersion == SecurityVersion.WSSecurity11)
            {
                IdManager = WSSecurityJan2004.IdManager.Instance;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(messageSecurityVersion), SR.MessageSecurityVersionOutOfRange));
            }

            WSUtilitySpecificationVersion = WSUtilitySpecificationVersion.Default;
            if (messageSecurityVersion.MessageSecurityTokenVersion.TrustVersion == TrustVersion.WSTrust13)
            {
                TrustDriver = new WSTrustDec2005.DriverDec2005(this);
            }
            else
            {
                TrustDriver = new WSTrustFeb2005.DriverFeb2005(this);
            }
        }

        public static SecurityStandardsManager DefaultInstance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new SecurityStandardsManager();
                }

                return s_instance;
            }
        }

        public SecurityVersion SecurityVersion
        {
            get { return MessageSecurityVersion?.SecurityVersion; }
        }

        public MessageSecurityVersion MessageSecurityVersion { get; }

        public TrustVersion TrustVersion
        {
            get { return MessageSecurityVersion?.TrustVersion; }
        }

        internal SecurityTokenSerializer SecurityTokenSerializer { get; }

        internal WSUtilitySpecificationVersion WSUtilitySpecificationVersion { get; }

        internal SignatureTargetIdManager IdManager { get; }

        internal SecureConversationDriver SecureConversationDriver { get; }

        internal TrustDriver TrustDriver { get; }

        private WSSecurityTokenSerializer WSSecurityTokenSerializer
        {
            get
            {
                if (_wsSecurityTokenSerializer == null)
                {
                    if (!(SecurityTokenSerializer is WSSecurityTokenSerializer wsSecurityTokenSerializer))
                    {
                        wsSecurityTokenSerializer = new WSSecurityTokenSerializer(SecurityVersion);
                    }

                    _wsSecurityTokenSerializer = wsSecurityTokenSerializer;
                }

                return _wsSecurityTokenSerializer;
            }
        }

        internal bool TryCreateKeyIdentifierClauseFromTokenXml(XmlElement element, SecurityTokenReferenceStyle tokenReferenceStyle, out SecurityKeyIdentifierClause securityKeyIdentifierClause)
        {
            return WSSecurityTokenSerializer.TryCreateKeyIdentifierClauseFromTokenXml(element, tokenReferenceStyle, out securityKeyIdentifierClause);
        }

        internal SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXml(XmlElement element, SecurityTokenReferenceStyle tokenReferenceStyle)
        {
            return WSSecurityTokenSerializer.CreateKeyIdentifierClauseFromTokenXml(element, tokenReferenceStyle);
        }

        internal SendSecurityHeader CreateSendSecurityHeader(Message message,
            string actor, bool mustUnderstand, bool relay,
            SecurityAlgorithmSuite algorithmSuite, MessageDirection direction)
        {
            return SecurityVersion.CreateSendSecurityHeader(message, actor, mustUnderstand, relay, this, algorithmSuite, direction);
        }

        internal ReceiveSecurityHeader TryCreateReceiveSecurityHeader(Message message,
            string actor,
            SecurityAlgorithmSuite algorithmSuite, MessageDirection direction)
        {
            return SecurityVersion.TryCreateReceiveSecurityHeader(message, actor, this, algorithmSuite, direction);
        }

        internal bool DoesMessageContainSecurityHeader(Message message)
        {
            return SecurityVersion.DoesMessageContainSecurityHeader(message);
        }

        internal bool TryGetSecurityContextIds(Message message, string[] actors, bool isStrictMode, ICollection<UniqueId> results)
        {
            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(results));
            }
            SecureConversationDriver driver = SecureConversationDriver;
            int securityHeaderIndex = SecurityVersion.FindIndexOfSecurityHeader(message, actors);
            if (securityHeaderIndex < 0)
            {
                return false;
            }
            bool addedContextIds = false;
            using (XmlDictionaryReader reader = message.Headers.GetReaderAtHeader(securityHeaderIndex))
            {
                if (!reader.IsStartElement())
                {
                    return false;
                }
                if (reader.IsEmptyElement)
                {
                    return false;
                }
                reader.ReadStartElement();
                while (reader.IsStartElement())
                {
                    if (driver.IsAtSecurityContextToken(reader))
                    {
                        results.Add(driver.GetSecurityContextTokenId(reader));
                        addedContextIds = true;
                        if (isStrictMode)
                        {
                            break;
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            return addedContextIds;
        }
    }
}
