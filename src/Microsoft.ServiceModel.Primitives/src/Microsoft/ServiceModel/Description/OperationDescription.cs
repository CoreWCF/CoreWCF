using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Collections.Generic;

namespace Microsoft.ServiceModel.Description
{
    public class OperationDescription
    {
        internal const string SessionOpenedAction = "http://schemas.microsoft.com/2011/02/session/onopen"; //Channels.WebSocketTransportSettings.ConnectionOpenedAction;
        private XmlName _name;
        private bool _isInitiating;
        private bool _isTerminating;
        bool _isSessionOpenNotificationEnabled;
        private ContractDescription _declaringContract;
        private FaultDescriptionCollection _faults;
        private MessageDescriptionCollection _messages;
        private KeyedByTypeCollection<IOperationBehavior> _behaviors;
        private Collection<Type> _knownTypes;
        private MethodInfo _beginMethod;
        private MethodInfo _endMethod;
        private MethodInfo _syncMethod;
        private MethodInfo _taskMethod;
        private bool _validateRpcWrapperName = true;
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

            _name = new XmlName(name, true /*isEncoded*/);
            if (declaringContract == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(declaringContract));
            }

            _declaringContract = declaringContract;
            _isInitiating = true;
            _isTerminating = false;
            _faults = new FaultDescriptionCollection();
            _messages = new MessageDescriptionCollection();
            _behaviors = new KeyedByTypeCollection<IOperationBehavior>();
            _knownTypes = new Collection<Type>();
        }

        internal bool HasNoDisposableParameters
        {
            get { return _hasNoDisposableParameters; }
            set { _hasNoDisposableParameters = value; }
        }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo SyncMethod
        {
            get { return _syncMethod; }
            set { _syncMethod = value; }
        }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo BeginMethod
        {
            get { return _beginMethod; }
            set { _beginMethod = value; }
        }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo EndMethod
        {
            get { return _endMethod; }
            set { _endMethod = value; }
        }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public MethodInfo TaskMethod
        {
            get { return _taskMethod; }
            set { _taskMethod = value; }
        }

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

        public FaultDescriptionCollection Faults
        {
            get { return _faults; }
        }

        public bool IsOneWay
        {
            get { return Messages.Count == 1; }
        }

        public Collection<Type> KnownTypes
        {
            get { return _knownTypes; }
        }

        // Messages[0] is the 'request' (first of MEP), and for non-oneway MEPs, Messages[1] is the 'response' (second of MEP)
        public MessageDescriptionCollection Messages
        {
            get { return _messages; }
        }

        internal string CodeName
        {
            get { return _name.DecodedName; }
        }

        public string Name
        {
            get { return _name.EncodedName; }
        }

        internal bool IsValidateRpcWrapperName { get { return _validateRpcWrapperName; } }

        public KeyedCollection<Type, IOperationBehavior> OperationBehaviors
        {
            get { return _behaviors; }
        }

        internal KeyedByTypeCollection<IOperationBehavior> Behaviors
        {
            get { return _behaviors; }
        }

        internal XmlName XmlName
        {
            get { return _name; }
        }

        internal bool IsInitiating
        {
            get { return _isInitiating; }
            set { _isInitiating = value; }
        }

        internal bool IsServerInitiated()
        {
            EnsureInvariants();
            return Messages[0].Direction == MessageDirection.Output;
        }

        internal bool IsTerminating
        {
            get { return _isTerminating; }
            set { _isTerminating = value; }
        }

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

        internal bool IsSessionOpenNotificationEnabled
        {
            get { return _isSessionOpenNotificationEnabled; }
            set { _isSessionOpenNotificationEnabled = value; }
        }
    }
}