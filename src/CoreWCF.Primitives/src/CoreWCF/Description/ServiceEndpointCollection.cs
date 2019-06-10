using System;
using System.Collections.ObjectModel;
using System.Xml;

namespace CoreWCF.Description
{
    public class ServiceEndpointCollection : Collection<ServiceEndpoint>
    {
        internal ServiceEndpointCollection()
        {
        }

        public ServiceEndpoint Find(Type contractType)
        {
            if (contractType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractType));
            }

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Contract.ContractType == contractType)
                {
                    return endpoint;
                }
            }

            return null;
        }

        public ServiceEndpoint Find(XmlQualifiedName contractName)
        {
            if (contractName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractName));
            }

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Contract.Name == contractName.Name && endpoint.Contract.Namespace == contractName.Namespace)
                {
                    return endpoint;
                }
            }

            return null;
        }

        public ServiceEndpoint Find(Type contractType, XmlQualifiedName bindingName)
        {
            if (contractType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractType));
            }
            if (bindingName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingName));
            }

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Contract.ContractType == contractType &&
                    endpoint.Binding.Name == bindingName.Name &&
                    endpoint.Binding.Namespace == bindingName.Namespace)
                {
                    return endpoint;
                }
            }

            return null;
        }

        public ServiceEndpoint Find(XmlQualifiedName contractName, XmlQualifiedName bindingName)
        {
            if (contractName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractName));
            }
            if (bindingName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingName));
            }

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Contract.Name == contractName.Name &&
                    endpoint.Contract.Namespace == contractName.Namespace &&
                    endpoint.Binding.Name == bindingName.Name &&
                    endpoint.Binding.Namespace == bindingName.Namespace)
                {
                    return endpoint;
                }
            }

            return null;
        }

        public ServiceEndpoint Find(Uri address)
        {
            if (address == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            }

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Address.Uri == address)
                {
                    return endpoint;
                }
            }

            return null;
        }

        public Collection<ServiceEndpoint> FindAll(Type contractType)
        {
            if (contractType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractType));
            }

            Collection<ServiceEndpoint> results = new Collection<ServiceEndpoint>();

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Contract.ContractType == contractType)
                {
                    results.Add(endpoint);
                }
            }

            return results;
        }

        public Collection<ServiceEndpoint> FindAll(XmlQualifiedName contractName)
        {
            if (contractName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractName));
            }

            Collection<ServiceEndpoint> results = new Collection<ServiceEndpoint>();

            foreach (ServiceEndpoint endpoint in this)
            {
                if (endpoint != null && endpoint.Contract.Name == contractName.Name && endpoint.Contract.Namespace == contractName.Namespace)
                {
                    results.Add(endpoint);
                }
            }

            return results;
        }

        protected override void InsertItem(int index, ServiceEndpoint item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, ServiceEndpoint item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }
            base.SetItem(index, item);
        }
    }

}