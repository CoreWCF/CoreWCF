using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Description;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class UniqueContractNameValidationBehavior : IServiceBehavior
    {
        Dictionary<XmlQualifiedName, ContractDescription> contracts = new Dictionary<XmlQualifiedName, ContractDescription>();

        public UniqueContractNameValidationBehavior() { }

        public void Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (description == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            if (serviceHostBase == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceHostBase));


            foreach (ServiceEndpoint endpoint in description.Endpoints)
            {
                XmlQualifiedName qname = new XmlQualifiedName(endpoint.Contract.Name, endpoint.Contract.Namespace);

                if (!contracts.ContainsKey(qname))
                {
                    contracts.Add(qname, endpoint.Contract);
                }
                else if (contracts[qname] != endpoint.Contract)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.SFxMultipleContractsWithSameName, qname.Name, qname.Namespace)));
                }
            }
        }

        public void AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
        }
    }

}