// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class DataContractSerializerServiceBehavior : IServiceBehavior, IEndpointBehavior
    {
        private int _maxItemsInObjectGraph;

        internal DataContractSerializerServiceBehavior(bool ignoreExtensionDataObject, int maxItemsInObjectGraph)
        {
            IgnoreExtensionDataObject = ignoreExtensionDataObject;
            _maxItemsInObjectGraph = maxItemsInObjectGraph;
        }

        public bool IgnoreExtensionDataObject { get; set; }

        public int MaxItemsInObjectGraph
        {
            get { return _maxItemsInObjectGraph; }
            set { _maxItemsInObjectGraph = value; }
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            ApplySerializationSettings(description, IgnoreExtensionDataObject, _maxItemsInObjectGraph);
        }

        void IEndpointBehavior.Validate(ServiceEndpoint serviceEndpoint)
        {
        }

        void IEndpointBehavior.AddBindingParameters(ServiceEndpoint serviceEndpoint, BindingParameterCollection parameters)
        {
        }

        void IEndpointBehavior.ApplyClientBehavior(ServiceEndpoint serviceEndpoint, ClientRuntime clientRuntime)
        {
            ApplySerializationSettings(serviceEndpoint, IgnoreExtensionDataObject, _maxItemsInObjectGraph);
        }

        void IEndpointBehavior.ApplyDispatchBehavior(ServiceEndpoint serviceEndpoint, EndpointDispatcher endpointDispatcher)
        {
            ApplySerializationSettings(serviceEndpoint, IgnoreExtensionDataObject, _maxItemsInObjectGraph);
        }

        internal static void ApplySerializationSettings(ServiceDescription description, bool ignoreExtensionDataObject, int maxItemsInObjectGraph)
        {
            foreach (ServiceEndpoint endpoint in description.Endpoints)
            {
                if (!endpoint.InternalIsSystemEndpoint(description))
                {
                    ApplySerializationSettings(endpoint, ignoreExtensionDataObject, maxItemsInObjectGraph);
                }
            }
        }

        internal static void ApplySerializationSettings(ServiceEndpoint endpoint, bool ignoreExtensionDataObject, int maxItemsInObjectGraph)
        {
            foreach (OperationDescription operation in endpoint.Contract.Operations)
            {
                foreach (IOperationBehavior ob in operation.Behaviors)
                {
                    DataContractSerializerOperationBehavior behavior = ob as DataContractSerializerOperationBehavior;
                    if (behavior != null)
                    {
                        if (!behavior.IgnoreExtensionDataObjectSetExplicit)
                        {
                            behavior.ignoreExtensionDataObject = ignoreExtensionDataObject;
                        }
                        if (!behavior.MaxItemsInObjectGraphSetExplicit)
                        {
                            behavior.maxItemsInObjectGraph = maxItemsInObjectGraph;
                        }
                    }
                }
            }
        }

    }

}