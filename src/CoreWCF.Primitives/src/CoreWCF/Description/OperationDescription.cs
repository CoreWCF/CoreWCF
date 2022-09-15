// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Reflection;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Description
{
    public class OperationDescription
    {
        internal const string SessionOpenedAction = "http://schemas.microsoft.com/2011/02/session/onopen"; //Channels.WebSocketTransportSettings.ConnectionOpenedAction;
        private ContractDescription _declaringContract;
        private bool _hasNoDisposableParameters;

        public OperationDescription(string name, ContractDescription declaringContract)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (name.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(name), SR.SFxOperationDescriptionNameCannotBeEmpty));
            }

            XmlName = new XmlName(name, true /*isEncoded*/);
            _declaringContract = declaringContract ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(declaringContract));
            IsInitiating = true;
            IsTerminating = false;
            Faults = new FaultDescriptionCollection();
            Messages = new MessageDescriptionCollection();
            Behaviors = new KeyedByTypeCollection<IOperationBehavior>();
            KnownTypes = new Collection<Type>();
            AuthorizeOperation = new KeyedByTypeCollection<IAuthorizeOperation>();
        }

        internal OperationDescription(string name, ContractDescription declaringContract, bool validateRpcWrapperName) : this(name, declaringContract)
        {
            IsValidateRpcWrapperName = validateRpcWrapperName;
        }

        internal bool HasNoDisposableParameters
        {
            get { return _hasNoDisposableParameters; }
            set { _hasNoDisposableParameters = value; }
        }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo SyncMethod { get; set; }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo BeginMethod { get; set; }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo EndMethod { get; set; }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo TaskMethod { get; set; }

        internal MethodInfo OperationMethod
        {
            get
            {
                if (SyncMethod == null)
                {
                    return TaskMethod ?? BeginMethod;
                }
                else
                {
                    return SyncMethod;
                }
            }
        }

        internal bool HasProtectionLevel => false;

        public ContractDescription DeclaringContract
        {
            get { return _declaringContract; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(DeclaringContract));
                }
                else
                {
                    _declaringContract = value;
                }
            }
        }

        public FaultDescriptionCollection Faults { get; }

        public bool IsOneWay
        {
            get { return Messages.Count == 1; }
        }

        public Collection<Type> KnownTypes { get; }

        // Messages[0] is the 'request' (first of MEP), and for non-oneway MEPs, Messages[1] is the 'response' (second of MEP)
        public MessageDescriptionCollection Messages { get; }

        internal string CodeName
        {
            get { return XmlName.DecodedName; }
        }

        public string Name
        {
            get { return XmlName.EncodedName; }
        }

        internal bool IsValidateRpcWrapperName { private set; get; } = true;

        internal KeyedByTypeCollection<IAuthorizeOperation> AuthorizeOperation { get; }

        public KeyedCollection<Type, IOperationBehavior> OperationBehaviors
        {
            get { return Behaviors; }
        }

        internal KeyedByTypeCollection<IOperationBehavior> Behaviors { get; }

        internal XmlName XmlName { get; }

        public bool IsInitiating { get; set; }

        internal bool IsServerInitiated()
        {
            EnsureInvariants();
            return Messages[0].Direction == MessageDirection.Output;
        }

        public bool IsTerminating { get; set; }

        internal void EnsureInvariants()
        {
            if (Messages.Count != 1 && Messages.Count != 2)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxOperationMustHaveOneOrTwoMessages, Name)));
            }
        }

        internal Type TaskTResult
        {
            get;
            set;
        }

        internal bool IsSessionOpenNotificationEnabled { get; set; }
    }
}
