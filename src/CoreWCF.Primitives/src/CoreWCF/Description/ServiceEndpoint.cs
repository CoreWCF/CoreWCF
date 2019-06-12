using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using CoreWCF.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class ServiceEndpoint
    {
        EndpointAddress _address;
        Binding _binding;
        ContractDescription _contract;
        Uri _listenUri;
        ListenUriMode _listenUriMode = ListenUriMode.Explicit;
        KeyedByTypeCollection<IEndpointBehavior> _behaviors;
        string _id;
        XmlName _name;
        bool _isEndpointFullyConfigured = false;

        public ServiceEndpoint(ContractDescription contract)
        {
            if (contract == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contract));
            _contract = contract;
        }

        public ServiceEndpoint(ContractDescription contract, Binding binding, EndpointAddress address)
        {
            if (contract == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contract));

            _contract = contract;
            _binding = binding;
            _address = address;
        }

        public EndpointAddress Address
        {
            get { return _address; }
            set { _address = value; }
        }

        public KeyedCollection<Type, IEndpointBehavior> EndpointBehaviors
        {
            get { return Behaviors; }
        }

        internal KeyedByTypeCollection<IEndpointBehavior> Behaviors
        {
            get
            {
                if (_behaviors == null)
                {
                    _behaviors = new KeyedByTypeCollection<IEndpointBehavior>();
                }

                return _behaviors;
            }
        }

        public Binding Binding
        {
            get { return _binding; }
            set { _binding = value; }
        }

        public ContractDescription Contract
        {
            get { return _contract; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                _contract = value;
            }
        }

        internal bool IsSystemEndpoint
        {
            get;
            set;
        }

        public string Name
        {
            get
            {
                if (!XmlName.IsNullOrEmpty(_name))
                {
                    return _name.EncodedName;
                }
                else if (_binding != null)
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}_{1}", new XmlName(Binding.Name).EncodedName, Contract.Name);
                }
                else
                {
                    return Contract.Name;
                }
            }
            set
            {
                _name = new XmlName(value, true /*isEncoded*/);
            }
        }

        internal Uri ListenUri
        {
            get
            {
                if (_listenUri == null)
                {
                    if (_address == null)
                    {
                        return null;
                    }
                    else
                    {
                        return _address.Uri;
                    }
                }
                else
                {
                    return _listenUri;
                }
            }
            set
            {
                if (value != null && !value.IsAbsoluteUri)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("value", SR.UriMustBeAbsolute);
                }
                _listenUri = value;
            }
        }

        internal ListenUriMode ListenUriMode
        {
            get { return _listenUriMode; }
            set
            {
                if (!ListenUriModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _listenUriMode = value;
            }
        }

        internal string Id
        {
            get
            {
                if (_id == null)
                    _id = Guid.NewGuid().ToString();
                return _id;
            }
        }

        internal Uri UnresolvedAddress
        {
            get;
            set;
        }

        internal Uri UnresolvedListenUri
        {
            get;
            set;
        }

        // This method ensures that the description object graph is structurally sound and that none
        // of the fundamental SFx framework assumptions have been violated.
        internal void EnsureInvariants()
        {
            if (Binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.AChannelServiceEndpointSBindingIsNull0));
            }
            if (Contract == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.AChannelServiceEndpointSContractIsNull0));
            }
            Contract.EnsureInvariants();
            //Binding.EnsureInvariants(Contract.Name);
        }

        internal void ValidateForClient()
        {
            Validate(true, false);
        }

        internal void ValidateForService(bool runOperationValidators)
        {
            Validate(runOperationValidators, true);
        }

        internal bool IsFullyConfigured
        {
            get { return _isEndpointFullyConfigured; }
            set { _isEndpointFullyConfigured = value; }
        }

        // for V1 legacy reasons, a mex endpoint is considered a system endpoint even if IsSystemEndpoint = false
        internal bool InternalIsSystemEndpoint(ServiceDescription description)
        {
            //if (ServiceMetadataBehavior.IsMetadataEndpoint(description, this))
            //{
            //    return true;
            //}
            return IsSystemEndpoint;
        }

        // This method runs validators (both builtin and ones in description).  
        // Precondition: EnsureInvariants() should already have been called.
        void Validate(bool runOperationValidators, bool isForService)
        {
            // contract behaviors
            ContractDescription contract = Contract;
            for (int j = 0; j < contract.ContractBehaviors.Count; j++)
            {
                IContractBehavior iContractBehavior = contract.ContractBehaviors[j];
                iContractBehavior.Validate(contract, this);
            }
            // endpoint behaviors
            for (int j = 0; j < EndpointBehaviors.Count; j++)
            {
                IEndpointBehavior ieb = EndpointBehaviors[j];
                ieb.Validate(this);
            }
            // operation behaviors
            if (runOperationValidators)
            {
                for (int j = 0; j < contract.Operations.Count; j++)
                {
                    OperationDescription op = contract.Operations[j];
                    TaskOperationDescriptionValidator.Validate(op, isForService);
                    for (int k = 0; k < op.OperationBehaviors.Count; k++)
                    {
                        IOperationBehavior iob = op.OperationBehaviors[k];
                        iob.Validate(op);
                    }
                }
            }
        }

    }
}