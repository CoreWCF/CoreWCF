using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Description
{
    public class ContractDescription
    {
        private Type _callbackContractType;
        private string _configurationName;
        private Type _contractType;
        private XmlName _name;
        private string _ns;
        private OperationDescriptionCollection _operations;
        private SessionMode _sessionMode;
        private KeyedByTypeCollection<IContractBehavior> _behaviors = new KeyedByTypeCollection<IContractBehavior>();
        //ProtectionLevel protectionLevel;
        //bool hasProtectionLevel;


        public ContractDescription(string name)
            : this(name, null)
        {
        }

        public ContractDescription(string name, string ns)
        {
            // the property setter validates given value
            Name = name;
            if (!string.IsNullOrEmpty(ns))
                NamingHelper.CheckUriParameter(ns, "ns");

            _operations = new OperationDescriptionCollection();
            _ns = ns ?? NamingHelper.DefaultNamespace; // ns can be ""
        }

        public string ConfigurationName
        {
            get { return _configurationName; }
            set { _configurationName = value; }
        }

        public Type ContractType
        {
            get { return _contractType; }
            set { _contractType = value; }
        }

        public Type CallbackContractType
        {
            get { return _callbackContractType; }
            set { _callbackContractType = value; }
        }

        public string Name
        {
            get { return _name.EncodedName; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.Length == 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ArgumentOutOfRangeException(nameof(value), SR.SFxContractDescriptionNameCannotBeEmpty));
                }
                _name = new XmlName(value, true /*isEncoded*/);
            }
        }

        public string Namespace
        {
            get { return _ns; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    NamingHelper.CheckUriProperty(value, "Namespace");
                _ns = value;
            }
        }

        public OperationDescriptionCollection Operations
        {
            get { return _operations; }
        }

        internal bool HasProtectionLevel => false;

        public SessionMode SessionMode
        {
            get { return _sessionMode; }
            set
            {
                if (!SessionModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _sessionMode = value;
            }
        }

        public KeyedCollection<Type, IContractBehavior> ContractBehaviors
        {
            get { return Behaviors; }
        }

        internal KeyedByTypeCollection<IContractBehavior> Behaviors
        {
            get { return _behaviors; }
        }

        public Collection<ContractDescription> GetInheritedContracts()
        {
            Collection<ContractDescription> result = new Collection<ContractDescription>();
            for (int i = 0; i < Operations.Count; i++)
            {
                OperationDescription od = Operations[i];
                if (od.DeclaringContract != this)
                {
                    ContractDescription inheritedContract = od.DeclaringContract;
                    if (!result.Contains(inheritedContract))
                    {
                        result.Add(inheritedContract);
                    }
                }
            }
            return result;
        }

        public static ContractDescription GetContract<TService>(Type contractType) where TService : class
        {
            if (contractType == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractType));

            var typeLoader = new TypeLoader<TService>();
            ContractDescription description = typeLoader.LoadContractDescription(contractType);
            return description;
        }

        public static ContractDescription GetContract<TService>(Type contractType, object serviceImplementation) where TService : class
        {
            if (contractType == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractType));

            if (serviceImplementation == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceImplementation));

            var typeLoader = new TypeLoader<TService>();
            ContractDescription description = typeLoader.LoadContractDescription(contractType, serviceImplementation);
            return description;
        }

        internal void EnsureInvariants()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.AChannelServiceEndpointSContractSNameIsNull0));
            }
            if (Namespace == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.AChannelServiceEndpointSContractSNamespace0));
            }
            if (Operations.Count == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxContractHasZeroOperations, Name)));
            }
            bool thereIsAtLeastOneInitiatingOperation = false;
            for (int i = 0; i < Operations.Count; i++)
            {
                OperationDescription operationDescription = Operations[i];
                operationDescription.EnsureInvariants();
                if (operationDescription.IsInitiating)
                    thereIsAtLeastOneInitiatingOperation = true;
                if ((!operationDescription.IsInitiating || operationDescription.IsTerminating)
                    && (SessionMode != SessionMode.Required))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.ContractIsNotSelfConsistentItHasOneOrMore2, Name)));
                }
            }
            if (!thereIsAtLeastOneInitiatingOperation)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxContractHasZeroInitiatingOperations, Name)));
            }
        }

        internal bool IsDuplex()
        {
            for (int i = 0; i < _operations.Count; ++i)
            {
                if (_operations[i].IsServerInitiated())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
