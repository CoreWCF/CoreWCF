// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Security;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    // This class is used to describe the message security requirements. It's only here as a placeholder
    // to allow minimal changes to ported code.
    public class ChannelProtectionRequirements
    {
        private bool _isReadOnly;

        public ChannelProtectionRequirements()
        {
            IncomingSignatureParts = new ScopedMessagePartSpecification();
            IncomingEncryptionParts = new ScopedMessagePartSpecification();
            OutgoingSignatureParts = new ScopedMessagePartSpecification();
            OutgoingEncryptionParts = new ScopedMessagePartSpecification();
        }

        public bool IsReadOnly => _isReadOnly;

        public ChannelProtectionRequirements(ChannelProtectionRequirements other)
        {
            if (other == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(other));

            IncomingSignatureParts = new ScopedMessagePartSpecification(other.IncomingSignatureParts);
            IncomingEncryptionParts = new ScopedMessagePartSpecification(other.IncomingEncryptionParts);
            OutgoingSignatureParts = new ScopedMessagePartSpecification(other.OutgoingSignatureParts);
            OutgoingEncryptionParts = new ScopedMessagePartSpecification(other.OutgoingEncryptionParts);
        }

        internal ChannelProtectionRequirements(ChannelProtectionRequirements other, ProtectionLevel newBodyProtectionLevel)
        {
            if (other == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(other));

            IncomingSignatureParts = new ScopedMessagePartSpecification(other.IncomingSignatureParts, newBodyProtectionLevel != ProtectionLevel.None);
            IncomingEncryptionParts = new ScopedMessagePartSpecification(other.IncomingEncryptionParts, newBodyProtectionLevel == ProtectionLevel.EncryptAndSign);
            OutgoingSignatureParts = new ScopedMessagePartSpecification(other.OutgoingSignatureParts, newBodyProtectionLevel != ProtectionLevel.None);
            OutgoingEncryptionParts = new ScopedMessagePartSpecification(other.OutgoingEncryptionParts, newBodyProtectionLevel == ProtectionLevel.EncryptAndSign);
        }


        internal ScopedMessagePartSpecification IncomingSignatureParts { get; }

        internal ScopedMessagePartSpecification IncomingEncryptionParts { get; }

        internal ScopedMessagePartSpecification OutgoingSignatureParts { get; }

        internal ScopedMessagePartSpecification OutgoingEncryptionParts { get; }

        public void Add(ChannelProtectionRequirements protectionRequirements)
        {
            Add(protectionRequirements, false);
        }

        public void Add(ChannelProtectionRequirements protectionRequirements, bool channelScopeOnly)
        {
            if (protectionRequirements == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(protectionRequirements));

            if (protectionRequirements.IncomingSignatureParts != null)
                IncomingSignatureParts.AddParts(protectionRequirements.IncomingSignatureParts.ChannelParts);
            if (protectionRequirements.IncomingEncryptionParts != null)
                IncomingEncryptionParts.AddParts(protectionRequirements.IncomingEncryptionParts.ChannelParts);
            if (protectionRequirements.OutgoingSignatureParts != null)
                OutgoingSignatureParts.AddParts(protectionRequirements.OutgoingSignatureParts.ChannelParts);
            if (protectionRequirements.OutgoingEncryptionParts != null)
                OutgoingEncryptionParts.AddParts(protectionRequirements.OutgoingEncryptionParts.ChannelParts);

            if (!channelScopeOnly)
            {
                AddActionParts(IncomingSignatureParts, protectionRequirements.IncomingSignatureParts);
                AddActionParts(IncomingEncryptionParts, protectionRequirements.IncomingEncryptionParts);
                AddActionParts(OutgoingSignatureParts, protectionRequirements.OutgoingSignatureParts);
                AddActionParts(OutgoingEncryptionParts, protectionRequirements.OutgoingEncryptionParts);
            }
        }

        private static void AddActionParts(ScopedMessagePartSpecification to, ScopedMessagePartSpecification from)
        {
            foreach (var action in from.Actions)
            {
                MessagePartSpecification p;
                if (from.TryGetParts(action, true, out p))
                    to.AddParts(p, action);
            }
        }

        public void MakeReadOnly()
        {
            if (!_isReadOnly)
            {
                IncomingSignatureParts.MakeReadOnly();
                IncomingEncryptionParts.MakeReadOnly();
                OutgoingSignatureParts.MakeReadOnly();
                OutgoingEncryptionParts.MakeReadOnly();
                _isReadOnly = true;
            }
        }

        internal static ChannelProtectionRequirements CreateFromContract(ContractDescription contract, ISecurityCapabilities bindingElement)
        {
            return CreateFromContract(contract, bindingElement.SupportedRequestProtectionLevel, bindingElement.SupportedResponseProtectionLevel);
        }

        private static MessagePartSpecification UnionMessagePartSpecifications(ScopedMessagePartSpecification actionParts)
        {
            var result = new MessagePartSpecification(false);
            foreach (var action in actionParts.Actions)
            {
                MessagePartSpecification parts;
                if (actionParts.TryGetParts(action, out parts))
                {
                    if (parts.IsBodyIncluded)
                    {
                        result.IsBodyIncluded = true;
                    }
                    foreach (var headerType in parts.HeaderTypes)
                    {
                        if (!result.IsHeaderIncluded(headerType.Name, headerType.Namespace))
                        {
                            result.HeaderTypes.Add(headerType);
                        }
                    }
                }
            }
            return result;
        }

        internal static ChannelProtectionRequirements CreateFromContractAndUnionResponseProtectionRequirements(ContractDescription contract, ISecurityCapabilities bindingElement)
        {
            var contractRequirements = CreateFromContract(contract, bindingElement.SupportedRequestProtectionLevel, bindingElement.SupportedResponseProtectionLevel);
            var result = new ChannelProtectionRequirements();

            result.OutgoingEncryptionParts.AddParts(UnionMessagePartSpecifications(contractRequirements.OutgoingEncryptionParts), MessageHeaders.WildcardAction);
            result.OutgoingSignatureParts.AddParts(UnionMessagePartSpecifications(contractRequirements.OutgoingSignatureParts), MessageHeaders.WildcardAction);
            contractRequirements.IncomingEncryptionParts.CopyTo(result.IncomingEncryptionParts);
            contractRequirements.IncomingSignatureParts.CopyTo(result.IncomingSignatureParts);
            return result;
        }

        internal static ChannelProtectionRequirements CreateFromContract(ContractDescription contract, ProtectionLevel defaultRequestProtectionLevel, ProtectionLevel defaultResponseProtectionLevel)
        {
            if (contract == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contract));

            var requirements = new ChannelProtectionRequirements();

            var contractScopeDefaultRequestProtectionLevel = ProtectionLevel.None;
            var contractScopeDefaultResponseProtectionLevel = ProtectionLevel.None;
            if (contract.HasProtectionLevel) // Currently always false
            {
                //contractScopeDefaultRequestProtectionLevel = contract.ProtectionLevel;
                //contractScopeDefaultResponseProtectionLevel = contract.ProtectionLevel;
            }
            else
            {
                contractScopeDefaultRequestProtectionLevel = defaultRequestProtectionLevel;
                contractScopeDefaultResponseProtectionLevel = defaultResponseProtectionLevel;
            }

            foreach (var operation in contract.Operations)
            {
                var operationScopeDefaultRequestProtectionLevel = ProtectionLevel.None;
                var operationScopeDefaultResponseProtectionLevel = ProtectionLevel.None;
                if (operation.HasProtectionLevel) // Currently always false
                {
                    //operationScopeDefaultRequestProtectionLevel = operation.ProtectionLevel;
                    //operationScopeDefaultResponseProtectionLevel = operation.ProtectionLevel;
                }
                else
                {
                    operationScopeDefaultRequestProtectionLevel = contractScopeDefaultRequestProtectionLevel;
                    operationScopeDefaultResponseProtectionLevel = contractScopeDefaultResponseProtectionLevel;
                }
                foreach (var message in operation.Messages)
                {
                    var messageScopeDefaultProtectionLevel = ProtectionLevel.None;
                    if (message.HasProtectionLevel)
                    {
                        //messageScopeDefaultProtectionLevel = message.ProtectionLevel;
                    }
                    else if (message.Direction == MessageDirection.Input)
                    {
                        messageScopeDefaultProtectionLevel = operationScopeDefaultRequestProtectionLevel;
                    }
                    else
                    {
                        messageScopeDefaultProtectionLevel = operationScopeDefaultResponseProtectionLevel;
                    }

                    var signedParts = new MessagePartSpecification();
                    var encryptedParts = new MessagePartSpecification();

                    // determine header protection requirements for message
                    foreach (var header in message.Headers)
                    {
                        AddHeaderProtectionRequirements(header, signedParts, encryptedParts, messageScopeDefaultProtectionLevel);
                    }

                    // determine body protection requirements for message
                    ProtectionLevel bodyProtectionLevel;
                    if (message.Body.Parts.Count > 0)
                    {
                        // initialize the body protection level to none. all the body parts will be
                        // unioned to get the effective body protection level
                        bodyProtectionLevel = ProtectionLevel.None;
                    }
                    else if (message.Body.ReturnValue != null)
                    {
                        if (!(message.Body.ReturnValue.GetType().Equals(typeof(MessagePartDescription))))
                        {
                            Fx.Assert("Only body return values are supported currently");
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.OnlyBodyReturnValuesSupported));
                        }
                        bodyProtectionLevel = messageScopeDefaultProtectionLevel; // MessagePartDescription.HasProtectionLevel currently always false
                        //MessagePartDescription desc = message.Body.ReturnValue;
                        //bodyProtectionLevel = desc.HasProtectionLevel ? desc.ProtectionLevel : messageScopeDefaultProtectionLevel;
                    }
                    else
                    {
                        bodyProtectionLevel = messageScopeDefaultProtectionLevel;
                    }

                    // determine body protection requirements for message
                    if (message.Body.Parts.Count > 0)
                    {
                        foreach (var body in message.Body.Parts)
                        {
                            var partProtectionLevel = messageScopeDefaultProtectionLevel; // MessagePartDescription.HasProtectionLevel currently always false
                            //ProtectionLevel partProtectionLevel = body.HasProtectionLevel ? body.ProtectionLevel : messageScopeDefaultProtectionLevel;
                            bodyProtectionLevel = ProtectionLevelHelper.Max(bodyProtectionLevel, partProtectionLevel);
                            if (bodyProtectionLevel == ProtectionLevel.EncryptAndSign)
                                break;
                        }
                    }
                    if (bodyProtectionLevel != ProtectionLevel.None)
                    {
                        signedParts.IsBodyIncluded = true;
                        if (bodyProtectionLevel == ProtectionLevel.EncryptAndSign)
                            encryptedParts.IsBodyIncluded = true;
                    }

                    // add requirements for message 
                    if (message.Direction == MessageDirection.Input)
                    {
                        requirements.IncomingSignatureParts.AddParts(signedParts, message.Action);
                        requirements.IncomingEncryptionParts.AddParts(encryptedParts, message.Action);
                    }
                    else
                    {
                        requirements.OutgoingSignatureParts.AddParts(signedParts, message.Action);
                        requirements.OutgoingEncryptionParts.AddParts(encryptedParts, message.Action);
                    }
                }
                if (operation.Faults != null)
                {
                    if (operation.IsServerInitiated())
                    {
                        AddFaultProtectionRequirements(operation.Faults, requirements, operationScopeDefaultRequestProtectionLevel, true);
                    }
                    else
                    {
                        AddFaultProtectionRequirements(operation.Faults, requirements, operationScopeDefaultResponseProtectionLevel, false);
                    }
                }
            }

            return requirements;
        }

        private static void AddHeaderProtectionRequirements(MessageHeaderDescription header, MessagePartSpecification signedParts,
            MessagePartSpecification encryptedParts, ProtectionLevel defaultProtectionLevel)
        {
            var p = defaultProtectionLevel; //header.HasProtectionLevel currently is always false;
            //ProtectionLevel p = header.HasProtectionLevel ? header.ProtectionLevel : defaultProtectionLevel;

            if (p != ProtectionLevel.None)
            {
                var headerName = new XmlQualifiedName(header.Name, header.Namespace);
                signedParts.HeaderTypes.Add(headerName);
                if (p == ProtectionLevel.EncryptAndSign)
                    encryptedParts.HeaderTypes.Add(headerName);
            }
        }

        private static void AddFaultProtectionRequirements(FaultDescriptionCollection faults, ChannelProtectionRequirements requirements, ProtectionLevel defaultProtectionLevel, bool addToIncoming)
        {
            if (faults == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(faults));
            if (requirements == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(requirements));

            foreach (var fault in faults)
            {
                var signedParts = new MessagePartSpecification();
                var encryptedParts = new MessagePartSpecification();
                var p = defaultProtectionLevel; // FaultDescription.HasProtectionLevel currently is always false
                //ProtectionLevel p = fault.HasProtectionLevel ? fault.ProtectionLevel : defaultProtectionLevel;
                if (p != ProtectionLevel.None)
                {
                    signedParts.IsBodyIncluded = true;
                    if (p == ProtectionLevel.EncryptAndSign)
                    {
                        encryptedParts.IsBodyIncluded = true;
                    }
                }
                if (addToIncoming)
                {
                    requirements.IncomingSignatureParts.AddParts(signedParts, fault.Action);
                    requirements.IncomingEncryptionParts.AddParts(encryptedParts, fault.Action);
                }
                else
                {
                    requirements.OutgoingSignatureParts.AddParts(signedParts, fault.Action);
                    requirements.OutgoingEncryptionParts.AddParts(encryptedParts, fault.Action);
                }
            }
        }

    }
}