// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public abstract class PolicyConversionContext
    {
        protected PolicyConversionContext(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoint));
            }

            Contract = endpoint.Contract;
        }

        public abstract BindingElementCollection BindingElements { get; }
        public virtual BindingParameterCollection BindingParameters => null;
        public ContractDescription Contract { get; }
        public abstract PolicyAssertionCollection GetBindingAssertions();
        public abstract PolicyAssertionCollection GetOperationBindingAssertions(OperationDescription operation);
        public abstract PolicyAssertionCollection GetMessageBindingAssertions(MessageDescription message);
        public abstract PolicyAssertionCollection GetFaultBindingAssertions(FaultDescription fault);

        internal static XmlElement FindAssertion(ICollection<XmlElement> assertions, string localName, string namespaceUri, bool remove)
        {
            XmlElement result = null;
            foreach (XmlElement assertion in assertions)
            {
                if ((assertion.LocalName == localName) &&
                    ((namespaceUri == null) || (assertion.NamespaceURI == namespaceUri)))
                {
                    result = assertion;
                    if (remove)
                    {
                        assertions.Remove(result);
                    }

                    break;
                }
            }

            return result;
        }

    }
}
