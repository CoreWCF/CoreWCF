// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography;
using System.Xml;
using CanonicalizationDriver = CoreWCF.IdentityModel.CanonicalizationDriver;
using Psha1DerivedKeyGenerator = CoreWCF.IdentityModel.Psha1DerivedKeyGenerator;

namespace CoreWCF.Security
{
    internal abstract class SspiNegotiationTokenAuthenticator : NegotiationTokenAuthenticator<SspiNegotiationTokenAuthenticatorState>
    {
        private string _defaultServiceBinding;

        protected SspiNegotiationTokenAuthenticator()
            : base()
        {
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy { get; set; }

        protected object ThisLock { get; } = new object();

        public string DefaultServiceBinding
        {
            get
            {
                if (_defaultServiceBinding == null)
                {
                    lock (ThisLock)
                    {
                        if (_defaultServiceBinding == null)
                        {
                            _defaultServiceBinding = SecurityUtils.GetSpnFromIdentity(
                                                            SecurityUtils.CreateWindowsIdentity(),
                                                            new EndpointAddress(ListenUri));
                        }
                    }
                }

                return _defaultServiceBinding;
            }
            set { _defaultServiceBinding = value; }
        }

        // abstract methods
        public abstract XmlDictionaryString NegotiationValueType { get; }
        protected abstract ReadOnlyCollection<IAuthorizationPolicy> ValidateSspiNegotiation(ISspiNegotiation sspiNegotiation);
        protected abstract SspiNegotiationTokenAuthenticatorState CreateSspiState(byte[] incomingBlob, string incomingValueTypeUri);

        // helpers
        protected virtual void IssueServiceToken(SspiNegotiationTokenAuthenticatorState sspiState, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies, out SecurityContextSecurityToken serviceToken, out WrappedKeySecurityToken proofToken,
            out int issuedKeySize)
        {
            UniqueId contextId = SecurityUtils.GenerateUniqueId();
            string id = SecurityUtils.GenerateId();
            if (sspiState.RequestedKeySize == 0)
            {
                issuedKeySize = SecurityAlgorithmSuite.DefaultSymmetricKeyLength;
            }
            else
            {
                issuedKeySize = sspiState.RequestedKeySize;
            }
            byte[] key = new byte[issuedKeySize / 8];
            CryptoHelper.FillRandomBytes(key);
            DateTime effectiveTime = DateTime.UtcNow;
            DateTime expirationTime = TimeoutHelper.Add(effectiveTime, ServiceTokenLifetime);
            serviceToken = IssueSecurityContextToken(contextId, id, key, effectiveTime, expirationTime, authorizationPolicies, EncryptStateInServiceToken);
            proofToken = new WrappedKeySecurityToken(string.Empty, key, sspiState.SspiNegotiation);
        }

        protected virtual void ValidateIncomingBinaryNegotiation(BinaryNegotiation incomingNego)
        {
            if (incomingNego == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.NoBinaryNegoToReceive)));
            }
            incomingNego.Validate(NegotiationValueType);
        }

        protected virtual BinaryNegotiation GetOutgoingBinaryNegotiation(ISspiNegotiation sspiNegotiation, byte[] outgoingBlob)
        {
            return new BinaryNegotiation(NegotiationValueType, outgoingBlob);
        }

        private static void AddToDigest(HashAlgorithm negotiationDigest, Stream stream)
        {
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            CanonicalizationDriver canonicalizer = new CanonicalizationDriver();
            canonicalizer.SetInput(stream);
            byte[] canonicalizedData = canonicalizer.GetBytes();
            lock (negotiationDigest)
            {
                negotiationDigest.TransformBlock(canonicalizedData, 0, canonicalizedData.Length, canonicalizedData, 0);
            }
        }

        private static void AddToDigest(SspiNegotiationTokenAuthenticatorState sspiState, RequestSecurityToken rst)
        {
            MemoryStream stream = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream);
            rst.RequestSecurityTokenXml.WriteTo(writer);
            writer.Flush();
            AddToDigest(sspiState.NegotiationDigest, stream);
        }

        private static void AddToDigest(SspiNegotiationTokenAuthenticatorState sspiState, RequestSecurityTokenResponse rstr, bool wasReceived)
        {
            MemoryStream stream = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream);
            if (wasReceived)
            {
                rstr.RequestSecurityTokenResponseXml.WriteTo(writer);
            }
            else
            {
                rstr.WriteTo(writer);
            }
            writer.Flush();
            AddToDigest(sspiState.NegotiationDigest, stream);
        }

        private static byte[] ComputeAuthenticator(SspiNegotiationTokenAuthenticatorState sspiState, byte[] key)
        {
            byte[] negotiationHash;
            lock (sspiState.NegotiationDigest)
            {
                sspiState.NegotiationDigest.TransformFinalBlock(CryptoHelper.EmptyBuffer, 0, 0);
                negotiationHash = sspiState.NegotiationDigest.Hash;
            }
            Psha1DerivedKeyGenerator generator = new Psha1DerivedKeyGenerator(key);
            return generator.GenerateDerivedKey(SecurityUtils.CombinedHashLabel, negotiationHash, 256, 0);
        }

        // overrides
        protected override bool IsMultiLegNegotiation
        {
            get
            {
                return true;
            }
        }

        protected override Binding GetNegotiationBinding(Binding binding)
        {
            return binding;
        }

        protected override MessageFilter GetListenerFilter()
        {
            return new SspiNegotiationFilter(this);
        }

        protected override BodyWriter ProcessRequestSecurityToken(Message request, RequestSecurityToken requestSecurityToken, out SspiNegotiationTokenAuthenticatorState negotiationState)
        {
            if (request == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(request));
            }
            if (requestSecurityToken == null)
            {
                throw TraceUtility.ThrowHelperArgumentNull(nameof(requestSecurityToken), request);
            }
            if (requestSecurityToken.RequestType != null && requestSecurityToken.RequestType != StandardsManager.TrustDriver.RequestTypeIssue)
            {
                throw TraceUtility.ThrowHelperWarning(new SecurityNegotiationException(SR.Format(SR.InvalidRstRequestType, requestSecurityToken.RequestType)), request);
            }
            BinaryNegotiation incomingNego = requestSecurityToken.GetBinaryNegotiation();
            ValidateIncomingBinaryNegotiation(incomingNego);
            negotiationState = CreateSspiState(incomingNego.GetNegotiationData(), incomingNego.ValueTypeUri);
            AddToDigest(negotiationState, requestSecurityToken);
            negotiationState.Context = requestSecurityToken.Context;
            if (requestSecurityToken.KeySize != 0)
            {
                WSTrust.Driver.ValidateRequestedKeySize(requestSecurityToken.KeySize, SecurityAlgorithmSuite);
            }
            negotiationState.RequestedKeySize = requestSecurityToken.KeySize;
            string appliesToNamespace;
            string appliesToName;
            requestSecurityToken.GetAppliesToQName(out appliesToName, out appliesToNamespace);
            if (appliesToName == AddressingStrings.EndpointReference && appliesToNamespace == request.Version.Addressing.Namespace)
            {
                DataContractSerializer serializer;
                if (request.Version.Addressing == AddressingVersion.WSAddressing10)
                {
                    serializer = DataContractSerializerDefaults.CreateSerializer(typeof(EndpointAddress10), DataContractSerializerDefaults.MaxItemsInObjectGraph);
                    negotiationState.AppliesTo = requestSecurityToken.GetAppliesTo<EndpointAddress10>(serializer).ToEndpointAddress();
                }
                else if (request.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                {
                    serializer = DataContractSerializerDefaults.CreateSerializer(typeof(EndpointAddressAugust2004), DataContractSerializerDefaults.MaxItemsInObjectGraph);
                    negotiationState.AppliesTo = requestSecurityToken.GetAppliesTo<EndpointAddressAugust2004>(serializer).ToEndpointAddress();
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, request.Version.Addressing)));
                }

                negotiationState.AppliesToSerializer = serializer;
            }
            return ProcessNegotiation(negotiationState, request, incomingNego);
        }

        protected override BodyWriter ProcessRequestSecurityTokenResponse(SspiNegotiationTokenAuthenticatorState negotiationState, Message request, RequestSecurityTokenResponse requestSecurityTokenResponse)
        {
            if (request == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(request));
            }
            if (requestSecurityTokenResponse == null)
            {
                throw TraceUtility.ThrowHelperArgumentNull(nameof(requestSecurityTokenResponse), request);
            }
            if (requestSecurityTokenResponse.Context != negotiationState.Context)
            {
                throw TraceUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.BadSecurityNegotiationContext)), request);
            }
            AddToDigest(negotiationState, requestSecurityTokenResponse, true);
            BinaryNegotiation incomingNego = requestSecurityTokenResponse.GetBinaryNegotiation();
            ValidateIncomingBinaryNegotiation(incomingNego);
            return ProcessNegotiation(negotiationState, request, incomingNego);
        }

        private BodyWriter ProcessNegotiation(SspiNegotiationTokenAuthenticatorState negotiationState, Message incomingMessage, BinaryNegotiation incomingNego)
        {
            ISspiNegotiation sspiNegotiation = negotiationState.SspiNegotiation;

            byte[] outgoingBlob = sspiNegotiation.GetOutgoingBlob(incomingNego.GetNegotiationData(),
                                                            SecurityUtils.GetChannelBindingFromMessage(incomingMessage),
                                                            ExtendedProtectionPolicy);

            if (sspiNegotiation.IsValidContext == false)
            {
                throw TraceUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.InvalidSspiNegotiation)), incomingMessage);
            }
            // if there is no blob to send back the nego must be complete from the server side
            if (outgoingBlob == null && sspiNegotiation.IsCompleted == false)
            {
                throw TraceUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.NoBinaryNegoToSend)), incomingMessage);
            }
            BinaryNegotiation outgoingBinaryNegotiation;
            if (outgoingBlob != null)
            {
                outgoingBinaryNegotiation = GetOutgoingBinaryNegotiation(sspiNegotiation, outgoingBlob);
            }
            else
            {
                outgoingBinaryNegotiation = null;
            }
            BodyWriter replyBody;
            if (sspiNegotiation.IsCompleted)
            {
                ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = ValidateSspiNegotiation(sspiNegotiation);
                SecurityContextSecurityToken serviceToken;
                WrappedKeySecurityToken proofToken;
                int issuedKeySize;
                IssueServiceToken(negotiationState, authorizationPolicies, out serviceToken, out proofToken, out issuedKeySize);
                negotiationState.SetServiceToken(serviceToken);

                SecurityKeyIdentifierClause externalTokenReference = IssuedSecurityTokenParameters.CreateKeyIdentifierClause(serviceToken, SecurityTokenReferenceStyle.External);
                SecurityKeyIdentifierClause internalTokenReference = IssuedSecurityTokenParameters.CreateKeyIdentifierClause(serviceToken, SecurityTokenReferenceStyle.Internal);

                RequestSecurityTokenResponse dummyRstr = new RequestSecurityTokenResponse(StandardsManager)
                {
                    Context = negotiationState.Context,
                    KeySize = issuedKeySize,
                    TokenType = SecurityContextTokenUri
                };
                if (outgoingBinaryNegotiation != null)
                {
                    dummyRstr.SetBinaryNegotiation(outgoingBinaryNegotiation);
                }
                dummyRstr.RequestedUnattachedReference = externalTokenReference;
                dummyRstr.RequestedAttachedReference = internalTokenReference;
                dummyRstr.SetLifetime(serviceToken.ValidFrom, serviceToken.ValidTo);
                if (negotiationState.AppliesTo != null)
                {
                    if (incomingMessage.Version.Addressing == AddressingVersion.WSAddressing10)
                    {
                        dummyRstr.SetAppliesTo<EndpointAddress10>(EndpointAddress10.FromEndpointAddress(
                            negotiationState.AppliesTo),
                            negotiationState.AppliesToSerializer);
                    }
                    else if (incomingMessage.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                    {
                        dummyRstr.SetAppliesTo<EndpointAddressAugust2004>(EndpointAddressAugust2004.FromEndpointAddress(
                            negotiationState.AppliesTo),
                            negotiationState.AppliesToSerializer);
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, incomingMessage.Version.Addressing)));
                    }
                }
                dummyRstr.MakeReadOnly();
                AddToDigest(negotiationState, dummyRstr, false);
                RequestSecurityTokenResponse negotiationRstr = new RequestSecurityTokenResponse(StandardsManager)
                {
                    RequestedSecurityToken = serviceToken,

                    RequestedProofToken = proofToken,
                    Context = negotiationState.Context,
                    KeySize = issuedKeySize,
                    TokenType = SecurityContextTokenUri
                };
                if (outgoingBinaryNegotiation != null)
                {
                    negotiationRstr.SetBinaryNegotiation(outgoingBinaryNegotiation);
                }
                negotiationRstr.RequestedAttachedReference = internalTokenReference;
                negotiationRstr.RequestedUnattachedReference = externalTokenReference;
                if (negotiationState.AppliesTo != null)
                {
                    if (incomingMessage.Version.Addressing == AddressingVersion.WSAddressing10)
                    {
                        negotiationRstr.SetAppliesTo<EndpointAddress10>(
                            EndpointAddress10.FromEndpointAddress(negotiationState.AppliesTo),
                            negotiationState.AppliesToSerializer);
                    }
                    else if (incomingMessage.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                    {
                        negotiationRstr.SetAppliesTo<EndpointAddressAugust2004>(
                            EndpointAddressAugust2004.FromEndpointAddress(negotiationState.AppliesTo),
                            negotiationState.AppliesToSerializer);
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, incomingMessage.Version.Addressing)));
                    }
                }
                negotiationRstr.MakeReadOnly();

                byte[] authenticator = ComputeAuthenticator(negotiationState, serviceToken.GetKeyBytes());
                RequestSecurityTokenResponse authenticatorRstr = new RequestSecurityTokenResponse(StandardsManager)
                {
                    Context = negotiationState.Context
                };
                authenticatorRstr.SetAuthenticator(authenticator);
                authenticatorRstr.MakeReadOnly();

                List<RequestSecurityTokenResponse> rstrList = new List<RequestSecurityTokenResponse>(2)
                {
                    negotiationRstr,
                    authenticatorRstr
                };
                replyBody = new RequestSecurityTokenResponseCollection(rstrList, StandardsManager);

            }
            else
            {
                RequestSecurityTokenResponse rstr = new RequestSecurityTokenResponse(StandardsManager)
                {
                    Context = negotiationState.Context
                };
                rstr.SetBinaryNegotiation(outgoingBinaryNegotiation);
                rstr.MakeReadOnly();
                AddToDigest(negotiationState, rstr, false);
                replyBody = rstr;
            }

            return replyBody;
        }

        private class SspiNegotiationFilter : HeaderFilter
        {
            private readonly SspiNegotiationTokenAuthenticator _authenticator;

            public SspiNegotiationFilter(SspiNegotiationTokenAuthenticator authenticator)
            {
                _authenticator = authenticator;
            }

            public override bool Match(Message message)
            {
                if (message.Headers.Action == _authenticator.RequestSecurityTokenAction.Value
                    || message.Headers.Action == _authenticator.RequestSecurityTokenResponseAction.Value)
                {
                    return !SecurityVersion.Default.DoesMessageContainSecurityHeader(message);
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
