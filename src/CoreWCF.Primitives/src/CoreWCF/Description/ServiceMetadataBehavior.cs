// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Description
{
    public class ServiceMetadataBehavior : IServiceBehavior
    {
        public const string MexContractName = "IMetadataExchange";
        internal const string MexContractNamespace = "http://schemas.microsoft.com/2006/04/mex";
        private Uri _httpGetUrl;
        private Uri _httpsGetUrl;
        private Uri _externalMetadataLocation = null;
        private MetadataExporter _metadataExporter = null;
        private static ContractDescription s_mexContract = null;
        private static readonly object s_thisLock = new object();

        public bool HttpGetEnabled { get; set; } = false;

        [TypeConverter(typeof(UriTypeConverter))]
        public Uri HttpGetUrl
        {
            get { return _httpGetUrl; }
            set
            {
                if (value != null && value.IsAbsoluteUri && value.Scheme != Uri.UriSchemeHttp)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxServiceMetadataBehaviorUrlMustBeHttpOrRelative,
                            nameof(HttpGetUrl), Uri.UriSchemeHttp, value.ToString(), value.Scheme));
                }

                _httpGetUrl = value;
            }
        }

        public bool HttpsGetEnabled { get; set; } = false;

        [TypeConverter(typeof(UriTypeConverter))]
        public Uri HttpsGetUrl
        {
            get { return _httpsGetUrl; }
            set
            {
                if (value != null && value.IsAbsoluteUri && value.Scheme != Uri.UriSchemeHttps)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxServiceMetadataBehaviorUrlMustBeHttpOrRelative,
                        nameof(HttpsGetUrl), Uri.UriSchemeHttps, value.ToString(), value.Scheme));
                }

                _httpsGetUrl = value;
            }
        }

        [TypeConverter(typeof(UriTypeConverter))]
        public Uri ExternalMetadataLocation
        {
            get { return _externalMetadataLocation; }
            set
            {
                if (value != null && value.IsAbsoluteUri && !(value.Scheme == Uri.UriSchemeHttp || value.Scheme == Uri.UriSchemeHttps))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(ExternalMetadataLocation), SR.Format(SR.SFxBadMetadataLocationUri, value.OriginalString, value.Scheme));
                }

                _externalMetadataLocation = value;
            }
        }

        public MetadataExporter MetadataExporter
        {
            get
            {
                if (_metadataExporter == null)
                {
                    _metadataExporter = new WsdlExporter();
                }

                return _metadataExporter;
            }
            set
            {
                _metadataExporter = value;
            }
        }

        internal static ContractDescription MexContract
        {
            get
            {
                EnsureMexContractDescription();
                return s_mexContract;
            }
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase) { }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters) { }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            if (serviceHostBase == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceHostBase));
            }

            ApplyBehavior(description, serviceHostBase);
        }

        private void ApplyBehavior(ServiceDescription description, ServiceHostBase host)
        {
            ServiceMetadataExtension mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(host);
            SetExtensionProperties(description, host, mex);
        }

        private void SetExtensionProperties(ServiceDescription description, ServiceHostBase host, ServiceMetadataExtension mex)
        {
            mex.ExternalMetadataLocation = ExternalMetadataLocation;
            mex.Initializer = new MetadataExtensionInitializer(this, description, host);
            mex.HttpGetEnabled = HttpGetEnabled;
            mex.HttpsGetEnabled = HttpsGetEnabled;

            mex.HttpGetUrl = host.GetVia(Uri.UriSchemeHttp, _httpGetUrl ?? new Uri(string.Empty, UriKind.Relative));
            mex.HttpsGetUrl = host.GetVia(Uri.UriSchemeHttps, _httpsGetUrl ?? new Uri(string.Empty, UriKind.Relative));

            UseRequestHeadersForMetadataAddressBehavior dynamicUpdateBehavior = description.Behaviors.Find<UseRequestHeadersForMetadataAddressBehavior>();
            if (dynamicUpdateBehavior != null)
            {
                mex.UpdateAddressDynamically = true;
                mex.UpdatePortsByScheme = new ReadOnlyDictionary<string, int>(dynamicUpdateBehavior.DefaultPortsByScheme);
            }

            foreach (ChannelDispatcherBase dispatcherBase in host.ChannelDispatchers)
            {
                if (dispatcherBase is ChannelDispatcher dispatcher && IsMetadataTransferDispatcher(description, dispatcher))
                {
                    mex.MexEnabled = true;
                    throw new NotImplementedException();
                    //mex.MexUrl = dispatcher.Listener.Uri;

                    //if (dynamicUpdateBehavior != null)
                    //{
                    //    foreach (EndpointDispatcher endpointDispatcher in dispatcher.Endpoints)
                    //    {
                    //        if (!endpointDispatcher.AddressFilterSetExplicit)
                    //        {
                    //            endpointDispatcher.AddressFilter = new MatchAllMessageFilter();
                    //        }
                    //    }
                    //}
                    //break;
                }
            }
        }

        private static EndpointDispatcher GetListenerByID(SynchronizedCollection<ChannelDispatcherBase> channelDispatchers, string id)
        {
            for (int i = 0; i < channelDispatchers.Count; ++i)
            {
                if (channelDispatchers[i] is ChannelDispatcher channelDispatcher)
                {
                    for (int j = 0; j < channelDispatcher.Endpoints.Count; ++j)
                    {
                        EndpointDispatcher endpointDispatcher = channelDispatcher.Endpoints[j];
                        if (endpointDispatcher.Id == id)
                        {
                            return endpointDispatcher;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsMetadataTransferDispatcher(ServiceDescription description, ChannelDispatcher channelDispatcher)
        {
            if (BehaviorMissingObjectNullOrServiceImplements(description, channelDispatcher))
            {
                return false;
            }

            foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
            {
                if (endpointDispatcher.ContractName == MexContractName
                    && endpointDispatcher.ContractNamespace == MexContractNamespace)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BehaviorMissingObjectNullOrServiceImplements(ServiceDescription description, object obj)
        {
            if (obj == null)
            {
                return true;
            }

            if (description.Behaviors != null && description.Behaviors.Find<ServiceMetadataBehavior>() == null)
            {
                return true;
            }

            if (description.ServiceType != null && description.ServiceType.GetInterface(typeof(IMetadataExchange).Name) != null)
            {
                return true;
            }

            return false;
        }

        internal static bool IsHttpGetMetadataDispatcher(ServiceDescription description, ChannelDispatcher channelDispatcher)
        {
            if (description.Behaviors.Find<ServiceMetadataBehavior>() == null)
            {
                return false;
            }

            foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
            {
                if (endpointDispatcher.ContractName == ServiceMetadataExtension.HttpGetImpl.ContractName
                    && endpointDispatcher.ContractNamespace == ServiceMetadataExtension.HttpGetImpl.ContractNamespace)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsMetadataEndpoint(ServiceDescription description, ServiceEndpoint endpoint)
        {
            if (BehaviorMissingObjectNullOrServiceImplements(description, endpoint))
            {
                return false;
            }

            return IsMetadataEndpoint(endpoint);

        }

        private static bool IsMetadataEndpoint(ServiceEndpoint endpoint)
        {
            return endpoint.Contract.Name == MexContractName
                    && endpoint.Contract.Namespace == MexContractNamespace;
        }

        internal static bool IsMetadataImplementedType(ServiceDescription description, Type type)
        {
            if (BehaviorMissingObjectNullOrServiceImplements(description, type))
            {
                return false;
            }

            return type == typeof(IMetadataExchange);
        }

        internal static bool IsMetadataImplementedType(Type type)
        {
            return type == typeof(IMetadataExchange);
        }

        private static void EnsureMexContractDescription()
        {
            if (s_mexContract == null)
            {
                lock (s_thisLock)
                {
                    if (s_mexContract == null)
                    {
                        s_mexContract = CreateMexContract();
                    }
                }
            }
        }

        private static ContractDescription CreateMexContract()
        {
            // Using ServiceMetadataBehavior as a dummy type to pass to GetContract as it doesn't actually need the type
            // to be passed.
            ContractDescription mexContract = ContractDescription.GetContract<ServiceMetadataBehavior>(typeof(IMetadataExchange));
            foreach (OperationDescription operation in mexContract.Operations)
            {
                ((KeyedByTypeCollection<IOperationBehavior>)operation.OperationBehaviors).Find<OperationBehaviorAttribute>().Impersonation = ImpersonationOption.Allowed;
            }

            mexContract.ContractBehaviors.Add(new ServiceMetadataContractBehavior(true));
            return mexContract;
        }

        internal class MetadataExtensionInitializer
        {
            private readonly ServiceMetadataBehavior _behavior;
            private readonly ServiceDescription _description;
            private readonly ServiceHostBase _host;
            private Exception _metadataGenerationException = null;

            internal MetadataExtensionInitializer(ServiceMetadataBehavior behavior, ServiceDescription description, ServiceHostBase host)
            {
                _behavior = behavior;
                _description = description;
                _host = host;
            }

            internal MetadataSet GenerateMetadata()
            {
                if (_behavior.ExternalMetadataLocation == null || _behavior.ExternalMetadataLocation.ToString() == string.Empty)
                {
                    if (_metadataGenerationException != null)
                    {
                        throw _metadataGenerationException;
                    }

                    try
                    {
                        MetadataExporter exporter = _behavior.MetadataExporter;
                        XmlQualifiedName serviceName = new XmlQualifiedName(_description.Name, _description.Namespace);
                        Collection<ServiceEndpoint> exportedEndpoints = new Collection<ServiceEndpoint>();
                        foreach (ServiceEndpoint endpoint in _description.Endpoints)
                        {
                            ServiceMetadataContractBehavior contractBehavior = ((KeyedByTypeCollection<IContractBehavior>)endpoint.Contract.ContractBehaviors).Find<ServiceMetadataContractBehavior>();

                            // if contract behavior exists, generate metadata when the behavior allows metadata generation
                            // if contract behavior doesn't exist, generate metadata only for non system endpoints
                            if ((contractBehavior != null && !contractBehavior.MetadataGenerationDisabled) ||
                                (contractBehavior == null && !endpoint.IsSystemEndpoint))
                            {
                                EndpointAddress address = null;
                                EndpointDispatcher endpointDispatcher = GetListenerByID(_host.ChannelDispatchers, endpoint.Id);
                                if (endpointDispatcher != null)
                                {
                                    address = endpointDispatcher.EndpointAddress;
                                }

                                ServiceEndpoint exportedEndpoint = new ServiceEndpoint(endpoint.Contract)
                                {
                                    Binding = endpoint.Binding,
                                    Name = endpoint.Name,
                                    Address = address
                                };
                                foreach (IEndpointBehavior behavior in endpoint.EndpointBehaviors)
                                {
                                    exportedEndpoint.EndpointBehaviors.Add(behavior);
                                }

                                exportedEndpoints.Add(exportedEndpoint);
                            }
                        }

                        if (exporter is WsdlExporter wsdlExporter)
                        {
                            // Pass the BindingParameterCollection into the ExportEndpoints method so that the binding parameters can be using to export WSDL correctly.
                            // The binding parameters are used in BuildChannelListener, during which they can modify the configuration of the channel in ways that might have to
                            // be communicated in the WSDL. For example, in the case of Multi-Auth, the AuthenticationSchemesBindingParameter is used during BuildChannelListener
                            // to set the AuthenticationSchemes supported by the virtual directory on the HttpTransportBindingElement.  These authentication schemes also need
                            // to be in the WSDL, so that clients know what authentication schemes are supported by the service.
                            Fx.Assert(_host != null, "ServiceHostBase field on MetadataExtensionInitializer should never be null.");
                            wsdlExporter.ExportEndpoints(exportedEndpoints, serviceName, GetBindingParameters(_host, exportedEndpoints));
                        }
                        else
                        {
                            foreach (ServiceEndpoint endpoint in exportedEndpoints)
                            {
                                exporter.ExportEndpoint(endpoint);
                            }
                        }

                        //if (exporter.Errors.Count > 0 && DiagnosticUtility.ShouldTraceWarning)
                        //{
                        //    TraceWsdlExportErrors(exporter);
                        //}

                        return exporter.GetGeneratedMetadata();
                    }
                    catch (Exception e)
                    {
                        _metadataGenerationException = e;
                        throw;
                    }
                }

                return null;
            }

            private BindingParameterCollection GetBindingParameters(ServiceHostBase serviceHost, Collection<ServiceEndpoint> endpoints)
            {
                BindingParameterCollection parameters = new BindingParameterCollection();
                foreach (IServiceBehavior behavior in serviceHost.Description.Behaviors)
                {
                    behavior.AddBindingParameters(serviceHost.Description, serviceHost, endpoints, parameters);
                }

                foreach (ServiceEndpoint endpoint in endpoints)
                {
                    AddBindingParametersForSecurityContractInformation(endpoint, parameters);
                    foreach (IContractBehavior icb in endpoint.Contract.ContractBehaviors)
                    {
                        icb.AddBindingParameters(endpoint.Contract, endpoint, parameters);
                    }

                    foreach (IEndpointBehavior ieb in endpoint.EndpointBehaviors)
                    {
                        ieb.AddBindingParameters(endpoint, parameters);
                    }

                    foreach (OperationDescription op in endpoint.Contract.Operations)
                    {
                        foreach (IOperationBehavior iob in op.OperationBehaviors)
                        {
                            iob.AddBindingParameters(op, parameters);
                        }
                    }
                }

                return parameters;
            }

            internal static void AddBindingParametersForSecurityContractInformation(ServiceEndpoint endpoint, BindingParameterCollection parameters)
            {
                // get Contract info security needs, and put in BindingParameterCollection
                ISecurityCapabilities isc = null;
                BindingElementCollection elements = endpoint.Binding.CreateBindingElements();
                for (int i = 0; i < elements.Count; ++i)
                {
                    if (!(elements[i] is ITransportTokenAssertionProvider))
                    {
                        ISecurityCapabilities tmp = elements[i].GetProperty<ISecurityCapabilities>(new BindingContext(new CustomBinding(), new BindingParameterCollection()));
                        if (tmp != null)
                        {
                            isc = tmp;
                            break;
                        }
                    }
                }

                if (isc != null)
                {
                    // ensure existence of binding parameter
                    ChannelProtectionRequirements requirements = parameters.Find<ChannelProtectionRequirements>();
                    if (requirements == null)
                    {
                        requirements = new ChannelProtectionRequirements();
                        parameters.Add(requirements);
                    }

                    MessageEncodingBindingElement encoding = elements.Find<MessageEncodingBindingElement>();
                    // use endpoint.Binding.Version
                    if (encoding != null && encoding.MessageVersion.Addressing == AddressingVersion.None)
                    {
                        // This binding does not support response actions, so...
                        requirements.Add(ChannelProtectionRequirements.CreateFromContractAndUnionResponseProtectionRequirements(endpoint.Contract, isc));
                    }
                    else
                    {
                        requirements.Add(ChannelProtectionRequirements.CreateFromContract(endpoint.Contract, isc));
                    }
                }
            }
        }
    }
}
