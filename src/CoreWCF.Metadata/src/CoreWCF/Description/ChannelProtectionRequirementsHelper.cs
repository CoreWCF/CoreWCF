// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Description
{
    // This code mirrors code in ChannelProtectionRequirements in Primitives. When ProtectionLevel is supported to enable message security, this code will need to be updated
    internal static class ChannelProtectionRequirementsHelper
    {
        internal static ChannelProtectionRequirements CreateFromContractAndUnionResponseProtectionRequirements(ContractDescription contract, ISecurityCapabilities bindingElement)
        {
            ChannelProtectionRequirements contractRequirements = CreateFromContract(contract, bindingElement.SupportedRequestProtectionLevel, bindingElement.SupportedResponseProtectionLevel);
            var result = new ChannelProtectionRequirements();

            result.OutgoingEncryptionParts.AddParts(UnionMessagePartSpecifications(contractRequirements.OutgoingEncryptionParts), MessageHeaders.WildcardAction);
            result.OutgoingSignatureParts.AddParts(UnionMessagePartSpecifications(contractRequirements.OutgoingSignatureParts), MessageHeaders.WildcardAction);
            contractRequirements.IncomingEncryptionParts.CopyTo(result.IncomingEncryptionParts);
            contractRequirements.IncomingSignatureParts.CopyTo(result.IncomingSignatureParts);
            return result;
        }

        internal static ChannelProtectionRequirements CreateFromContract(ContractDescription contract, ISecurityCapabilities bindingElement)
        {
            return CreateFromContract(contract, bindingElement.SupportedRequestProtectionLevel, bindingElement.SupportedResponseProtectionLevel);
        }

        private static ChannelProtectionRequirements CreateFromContract(ContractDescription contract, ProtectionLevel defaultRequestProtectionLevel, ProtectionLevel defaultResponseProtectionLevel)
        {
            if (contract == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contract));
            }

            var requirements = new ChannelProtectionRequirements();

            ProtectionLevel contractScopeDefaultRequestProtectionLevel = ProtectionLevel.None;
            ProtectionLevel contractScopeDefaultResponseProtectionLevel = ProtectionLevel.None;
            //if (contract.HasProtectionLevel) // Currently always false
            //{
            //    //contractScopeDefaultRequestProtectionLevel = contract.ProtectionLevel;
            //    //contractScopeDefaultResponseProtectionLevel = contract.ProtectionLevel;
            //}
            //else
            //{
            contractScopeDefaultRequestProtectionLevel = defaultRequestProtectionLevel;
            contractScopeDefaultResponseProtectionLevel = defaultResponseProtectionLevel;
            //}

            foreach (OperationDescription operation in contract.Operations)
            {
                ProtectionLevel operationScopeDefaultRequestProtectionLevel = ProtectionLevel.None;
                ProtectionLevel operationScopeDefaultResponseProtectionLevel = ProtectionLevel.None;
                //if (operation.HasProtectionLevel) // Currently always false
                //{
                //    //operationScopeDefaultRequestProtectionLevel = operation.ProtectionLevel;
                //    //operationScopeDefaultResponseProtectionLevel = operation.ProtectionLevel;
                //}
                //else
                //{
                operationScopeDefaultRequestProtectionLevel = contractScopeDefaultRequestProtectionLevel;
                operationScopeDefaultResponseProtectionLevel = contractScopeDefaultResponseProtectionLevel;
                //}
                foreach (MessageDescription message in operation.Messages)
                {
                    ProtectionLevel messageScopeDefaultProtectionLevel = ProtectionLevel.None;
                    //if (message.HasProtectionLevel)
                    //{
                    //    //messageScopeDefaultProtectionLevel = message.ProtectionLevel;
                    //}
                    //else
                    if (message.Direction == MessageDirection.Input)
                    {
                        messageScopeDefaultProtectionLevel = operationScopeDefaultRequestProtectionLevel;
                    }
                    else
                    {
                        messageScopeDefaultProtectionLevel = operationScopeDefaultResponseProtectionLevel;
                    }

                    var signedParts = new MessagePartSpecification();
                    var encryptedParts = new MessagePartSpecification();

                    // No-op until we support ProtectionLevel
                    // determine header protection requirements for message
                    //foreach (MessageHeaderDescription header in message.Headers)
                    //{
                    //    AddHeaderProtectionRequirements(header, signedParts, encryptedParts, messageScopeDefaultProtectionLevel);
                    //}

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

                    // This is no-op for now until we support message protection
                    // determine body protection requirements for message
                    //if (message.Body.Parts.Count > 0)
                    //{
                    //    foreach (MessagePartDescription body in message.Body.Parts)
                    //    {
                    //        ProtectionLevel partProtectionLevel = messageScopeDefaultProtectionLevel; // MessagePartDescription.HasProtectionLevel currently always false
                    //        //ProtectionLevel partProtectionLevel = body.HasProtectionLevel ? body.ProtectionLevel : messageScopeDefaultProtectionLevel;
                    //        bodyProtectionLevel = ProtectionLevelHelper.Max(bodyProtectionLevel, partProtectionLevel);
                    //        if (bodyProtectionLevel == ProtectionLevel.EncryptAndSign)
                    //        {
                    //            break;
                    //        }
                    //    }
                    //}
                    //if (bodyProtectionLevel != ProtectionLevel.None)
                    //{
                    //    signedParts.IsBodyIncluded = true;
                    //    if (bodyProtectionLevel == ProtectionLevel.EncryptAndSign)
                    //    {
                    //        encryptedParts.IsBodyIncluded = true;
                    //    }
                    //}

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

        private static void AddFaultProtectionRequirements(FaultDescriptionCollection faults, ChannelProtectionRequirements requirements, ProtectionLevel defaultProtectionLevel, bool addToIncoming)
        {
            if (faults == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(faults));
            }

            if (requirements == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(requirements));
            }

            foreach (FaultDescription fault in faults)
            {
                var signedParts = new MessagePartSpecification();
                var encryptedParts = new MessagePartSpecification();
                ProtectionLevel p = defaultProtectionLevel; // FaultDescription.HasProtectionLevel currently is always false
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

        private static MessagePartSpecification UnionMessagePartSpecifications(ScopedMessagePartSpecification actionParts)
        {
            MessagePartSpecification result = new MessagePartSpecification(false);
            foreach (string action in actionParts.Actions)
            {
                MessagePartSpecification parts;
                if (actionParts.TryGetParts(action, out parts))
                {
                    if (parts.IsBodyIncluded)
                    {
                        result.IsBodyIncluded = true;
                    }
                    foreach (XmlQualifiedName headerType in parts.HeaderTypes)
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

        #region Extension Methods
        private static void CopyTo(this ScopedMessagePartSpecification thisPtr, ScopedMessagePartSpecification target)
        {
            if (target == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(target));
            }
            target.ChannelParts.IsBodyIncluded = thisPtr.ChannelParts.IsBodyIncluded;
            foreach (XmlQualifiedName headerType in thisPtr.ChannelParts.HeaderTypes)
            {
                if (!target.ChannelParts.IsHeaderIncluded(headerType.Name, headerType.Namespace))
                {
                    target.ChannelParts.HeaderTypes.Add(headerType);
                }
            }
            foreach (string action in thisPtr.Actions)
            {
                if (thisPtr.TryGetParts(action, true, out var part))
                {
                    target.AddParts(part, action);
                }
                else
                {
                    Fx.Assert("Action should have been found in parts");
                }
            }
        }

        private static bool IsHeaderIncluded(this MessagePartSpecification thisPtr, string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }

            var headerTypes = thisPtr.HeaderTypes as IList<XmlQualifiedName>;
            Fx.Assert(headerTypes != null, "HeaderTypes should derive from IList<XmlQualifiedName");
            if (headerTypes != null)
            {
                for (int i = 0; i < headerTypes.Count; i++)
                {
                    XmlQualifiedName qname = headerTypes[i];
                    // Name is an optional attribute. If not present, compare with only the namespace.
                    if (string.IsNullOrEmpty(qname.Name))
                    {
                        if (qname.Namespace == ns)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (qname.Name == name && qname.Namespace == ns)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsServerInitiated(this OperationDescription thisPtr)
        {
            return thisPtr.Messages[0].Direction == MessageDirection.Output;
        }
        #endregion
    }
}
