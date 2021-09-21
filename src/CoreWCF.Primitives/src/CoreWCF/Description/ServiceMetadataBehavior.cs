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
        private static readonly Uri emptyUri = new Uri(String.Empty, UriKind.Relative);
        private Uri _httpGetUrl;
        private Uri _httpsGetUrl;
        private readonly Binding _httpGetBinding;
        private readonly Binding _httpsGetBinding;
        private Uri _externalMetadataLocation = null;
        private MetadataExporter _metadataExporter = null;
        private static ContractDescription s_mexContract = null;
        private static object s_thisLock = new object();

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

        //public Binding HttpGetBinding
        //{
        //    get { return this.httpGetBinding; }
        //    set
        //    {
        //        if (value != null)
        //        {
        //            if (!value.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxBindingSchemeDoesNotMatch,
        //                    value.Scheme, value.GetType().ToString(), Uri.UriSchemeHttp));
        //            }
        //            CustomBinding customBinding = new CustomBinding(value);
        //            TextMessageEncodingBindingElement textMessageEncodingBindingElement = customBinding.Elements.Find<TextMessageEncodingBindingElement>();
        //            if (textMessageEncodingBindingElement != null && !textMessageEncodingBindingElement.MessageVersion.IsMatch(MessageVersion.None))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxIncorrectMessageVersion,
        //                    textMessageEncodingBindingElement.MessageVersion.ToString(), MessageVersion.None.ToString()));
        //            }
        //            HttpTransportBindingElement httpTransportBindingElement = customBinding.Elements.Find<HttpTransportBindingElement>();
        //            if (httpTransportBindingElement != null)
        //            {
        //                httpTransportBindingElement.Method = "GET";
        //            }
        //            this.httpGetBinding = customBinding;
        //        }
        //    }
        //}

        //public Binding HttpsGetBinding
        //{
        //    get { return this.httpsGetBinding; }
        //    set
        //    {
        //        if (value != null)
        //        {
        //            if (!value.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.GetString(SR.SFxBindingSchemeDoesNotMatch,
        //                    value.Scheme, value.GetType().ToString(), Uri.UriSchemeHttps));
        //            }
        //            CustomBinding customBinding = new CustomBinding(value);
        //            TextMessageEncodingBindingElement textMessageEncodingBindingElement = customBinding.Elements.Find<TextMessageEncodingBindingElement>();
        //            if (textMessageEncodingBindingElement != null && !textMessageEncodingBindingElement.MessageVersion.IsMatch(MessageVersion.None))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.GetString(SR.SFxIncorrectMessageVersion,
        //                    textMessageEncodingBindingElement.MessageVersion.ToString(), MessageVersion.None.ToString()));
        //            }
        //            HttpsTransportBindingElement httpsTransportBindingElement = customBinding.Elements.Find<HttpsTransportBindingElement>();
        //            if (httpsTransportBindingElement != null)
        //            {
        //                httpsTransportBindingElement.Method = "GET";
        //            }
        //            this.httpsGetBinding = customBinding;
        //        }
        //    }
        //}

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
                    _metadataExporter = new WsdlExporter();

                return _metadataExporter;
            }
            set
            {
                _metadataExporter = value;
            }
        }

        static internal ContractDescription MexContract
        {
            get
            {
                EnsureMexContractDescription();
                return s_mexContract;
            }
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (description == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            if (serviceHostBase == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceHostBase));

            ApplyBehavior(description, serviceHostBase);
        }

        private void ApplyBehavior(ServiceDescription description, ServiceHostBase host)
        {
            ServiceMetadataExtension mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(description, host);
            SetExtensionProperties(description, host, mex);
            CustomizeMetadataEndpoints(description, host, mex);
            //CreateHttpGetEndpoints(description, host, mex);
        }

        //private void CreateHttpGetEndpoints(ServiceDescription description, ServiceHostBase host, ServiceMetadataExtension mex)
        //{
        //    // TODO: Wire up to aspnetcore pipeline
        //    bool httpDispatcherEnabled = false;
        //    bool httpsDispatcherEnabled = false;

        //    if (HttpGetEnabled)
        //    {
        //        httpDispatcherEnabled = EnsureGetDispatcher(host, mex, _httpGetUrl, Uri.UriSchemeHttp);
        //    }

        //    if (HttpsGetEnabled)
        //    {
        //        httpsDispatcherEnabled = EnsureGetDispatcher(host, mex, _httpsGetUrl, Uri.UriSchemeHttps);
        //    }

        //    if (!httpDispatcherEnabled && !httpsDispatcherEnabled)
        //    {
        //        if (HttpGetEnabled)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxServiceMetadataBehaviorNoHttpBaseAddress));
        //        }

        //        if (HttpsGetEnabled)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxServiceMetadataBehaviorNoHttpsBaseAddress));
        //        }
        //    }
        //}

        private static bool EnsureGetDispatcher(ServiceHostBase host, ServiceMetadataExtension mex, Uri url, string scheme)
        {
            throw new NotImplementedException("Need to wire up more directly to aspnetcore");
            //UriSchemeKeyedCollection baseAddresses = new UriSchemeKeyedCollection(host.BaseAddresses.ToArray());
            //Uri address = host.GetVia(scheme, url ?? new Uri(string.Empty, UriKind.Relative));

            //if (address != null)
            //{
            //    ChannelDispatcher channelDispatcher = mex.EnsureGetDispatcher(address, false /* isServiceDebugBehavior */);
            //    ((ServiceMetadataExtension.HttpGetImpl)channelDispatcher.Endpoints[0].DispatchRuntime.SingletonInstanceContext.UserObject).GetWsdlEnabled = true;
            //    return true;
            //}

            //return false;
        }

        private void SetExtensionProperties(ServiceDescription description, ServiceHostBase host, ServiceMetadataExtension mex)
        {
            mex.ExternalMetadataLocation = ExternalMetadataLocation;
            mex.Initializer = new MetadataExtensionInitializer(this, description, host);
            mex.HttpGetEnabled = HttpGetEnabled;
            mex.HttpsGetEnabled = HttpsGetEnabled;

            mex.HttpGetUrl = host.GetVia(Uri.UriSchemeHttp, _httpGetUrl == null ? new Uri(string.Empty, UriKind.Relative) : _httpGetUrl);
            mex.HttpsGetUrl = host.GetVia(Uri.UriSchemeHttps, _httpsGetUrl == null ? new Uri(string.Empty, UriKind.Relative) : _httpsGetUrl);

            mex.HttpGetBinding = _httpGetBinding;
            mex.HttpsGetBinding = _httpsGetBinding;

            //UseRequestHeadersForMetadataAddressBehavior dynamicUpdateBehavior = description.Behaviors.Find<UseRequestHeadersForMetadataAddressBehavior>();
            //if (dynamicUpdateBehavior != null)
            //{
            //    mex.UpdateAddressDynamically = true;
            //    mex.UpdatePortsByScheme = new Dictionary<string, int>(dynamicUpdateBehavior.DefaultPortsByScheme);
            //}

            foreach (ChannelDispatcherBase dispatcherBase in host.ChannelDispatchers)
            {
                ChannelDispatcher dispatcher = dispatcherBase as ChannelDispatcher;
                if (dispatcher != null && IsMetadataTransferDispatcher(description, dispatcher))
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

        private static void CustomizeMetadataEndpoints(ServiceDescription description, ServiceHostBase host, ServiceMetadataExtension mex)
        {
            //for (int i = 0; i < host.ChannelDispatchers.Count; i++)
            //{
            //    ChannelDispatcher channelDispatcher = host.ChannelDispatchers[i] as ChannelDispatcher;
            //    if (channelDispatcher != null && ServiceMetadataBehavior.IsMetadataTransferDispatcher(description, channelDispatcher))
            //    {
            //        if (channelDispatcher.Endpoints.Count != 1)
            //        {
            //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
            //                new InvalidOperationException(SR.Format(SR.SFxServiceMetadataBehaviorInstancingError, channelDispatcher.ListenUri, channelDispatcher.CreateContractListString())));
            //        }

            //        DispatchRuntime dispatcher = channelDispatcher.Endpoints[0].DispatchRuntime;

            //        // set instancing
            //        dispatcher.InstanceContextProvider =
            //           InstanceContextProviderBase.GetProviderForMode(InstanceContextMode.Single, dispatcher);

            //        bool isListeningOnHttps = channelDispatcher.ListenUri.Scheme == Uri.UriSchemeHttps;
            //        Uri listenUri = channelDispatcher.ListenUri;
            //        ServiceMetadataExtension.WSMexImpl impl = new ServiceMetadataExtension.WSMexImpl(mex, isListeningOnHttps, listenUri);
            //        dispatcher.SingletonInstanceContext = new InstanceContext(host, impl, false);
            //    }
            //}
        }

        private static EndpointDispatcher GetListenerFromEndpoint(SynchronizedCollection<ChannelDispatcherBase> channelDispatchers, ServiceEndpoint endpoint)
        {
            // I believe this should be sufficient to find the correct EndpointDispatcher, but it's a heuristic so it's possible it's insufficient
            for (int i = 0; i < channelDispatchers.Count; ++i)
            {
                ChannelDispatcher channelDispatcher = channelDispatchers[i] as ChannelDispatcher;
                if (channelDispatcher != null)
                {
                    for (int j = 0; j < channelDispatcher.Endpoints.Count; ++j)
                    {
                        EndpointDispatcher endpointDispatcher = channelDispatcher.Endpoints[j];
                        var dispatcherOperations = endpointDispatcher.DispatchRuntime.Operations;
                        var contractOperations = endpoint.Contract.Operations;
                        if (!CompareOperations(dispatcherOperations, contractOperations))
                        {
                            continue;
                        }

                        return endpointDispatcher;
                    }
                }
            }
            return null;
        }

        private static bool CompareOperations(SynchronizedKeyedCollection<string, DispatchOperation> dispatcherOperations, OperationDescriptionCollection contractOperations)
        {
            if (dispatcherOperations.Count != contractOperations.Count)
            {
                return false;
            }

            foreach(var contractOp in contractOperations)
            {
                if (!dispatcherOperations.Contains(contractOp.Name))
                {
                    return false;
                }
                var dispatchOp = dispatcherOperations[contractOp.Name];
                if (contractOp.Messages[0].Action != dispatchOp.Action || (!contractOp.IsOneWay && contractOp.Messages[1].Action != dispatchOp.ReplyAction))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsMetadataDispatcher(ServiceDescription description, ChannelDispatcher channelDispatcher)
        {
            foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
            {
                if (IsMetadataTransferDispatcher(description, channelDispatcher)
                    || IsHttpGetMetadataDispatcher(description, channelDispatcher))
                    return true;
            }
            return false;
        }

        private static bool IsMetadataTransferDispatcher(ServiceDescription description, ChannelDispatcher channelDispatcher)
        {
            if (BehaviorMissingObjectNullOrServiceImplements(description, channelDispatcher))
                return false;

            foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
            {
                if (endpointDispatcher.ContractName == MexContractName
                    && endpointDispatcher.ContractNamespace == MexContractNamespace)
                    return true;
            }
            return false;
        }

        private static bool BehaviorMissingObjectNullOrServiceImplements(ServiceDescription description, object obj)
        {
            if (obj == null)
                return true;
            if (description.Behaviors != null && description.Behaviors.Find<ServiceMetadataBehavior>() == null)
                return true;
            if (description.ServiceType != null && description.ServiceType.GetInterface(typeof(IMetadataExchange).Name) != null)
                return true;

            return false;
        }

        internal static bool IsHttpGetMetadataDispatcher(ServiceDescription description, ChannelDispatcher channelDispatcher)
        {
            if (description.Behaviors.Find<ServiceMetadataBehavior>() == null)
                return false;

            foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
            {
                if (endpointDispatcher.ContractName == ServiceMetadataExtension.HttpGetImpl.ContractName
                    && endpointDispatcher.ContractNamespace == ServiceMetadataExtension.HttpGetImpl.ContractNamespace)
                    return true;
            }
            return false;
        }

        internal static bool IsMetadataEndpoint(ServiceDescription description, ServiceEndpoint endpoint)
        {
            if (BehaviorMissingObjectNullOrServiceImplements(description, endpoint))
                return false;

            return IsMetadataEndpoint(endpoint);

        }

        private static bool IsMetadataEndpoint(ServiceEndpoint endpoint)
        {
            return (endpoint.Contract.Name == MexContractName
                    && endpoint.Contract.Namespace == MexContractNamespace);
        }

        internal static bool IsMetadataImplementedType(ServiceDescription description, Type type)
        {
            if (BehaviorMissingObjectNullOrServiceImplements(description, type))
                return false;

            return type == typeof(IMetadataExchange);
        }

        internal static bool IsMetadataImplementedType(Type type)
        {
            return type == typeof(IMetadataExchange);
        }

        //internal void AddImplementedContracts(ServiceHostBase.ServiceAndBehaviorsContractResolver resolver)
        //{
        //    if (!resolver.BehaviorContracts.ContainsKey(MexContractName))
        //    {
        //        resolver.BehaviorContracts.Add(MexContractName, MexContract);
        //    }
        //}

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
            private ServiceMetadataBehavior behavior;
            private ServiceDescription description;
            private ServiceHostBase host;
            private Exception metadataGenerationException = null;

            internal MetadataExtensionInitializer(ServiceMetadataBehavior behavior, ServiceDescription description, ServiceHostBase host)
            {
                this.behavior = behavior;
                this.description = description;
                this.host = host;
            }

            internal MetadataSet GenerateMetadata()
            {
                if (behavior.ExternalMetadataLocation == null || behavior.ExternalMetadataLocation.ToString() == string.Empty)
                {
                    if (metadataGenerationException != null)
                        throw metadataGenerationException;

                    try
                    {
                        MetadataExporter exporter = behavior.MetadataExporter;
                        XmlQualifiedName serviceName = new XmlQualifiedName(description.Name, description.Namespace);
                        Collection<ServiceEndpoint> exportedEndpoints = new Collection<ServiceEndpoint>();
                        foreach (ServiceEndpoint endpoint in description.Endpoints)
                        {
                            ServiceMetadataContractBehavior contractBehavior = ((KeyedByTypeCollection<IContractBehavior>)endpoint.Contract.ContractBehaviors).Find<ServiceMetadataContractBehavior>();

                            // if contract behavior exists, generate metadata when the behavior allows metadata generation
                            // if contract behavior doesn't exist, generate metadata only for non system endpoints
                            if ((contractBehavior != null && !contractBehavior.MetadataGenerationDisabled) ||
                                (contractBehavior == null && !endpoint.IsSystemEndpoint))
                            {
                                EndpointAddress address = null;
                                //EndpointDispatcher endpointDispatcher = GetListenerByID(host.ChannelDispatchers, endpoint.Id);
                                EndpointDispatcher endpointDispatcher = GetListenerFromEndpoint(host.ChannelDispatchers, endpoint);
                                if (endpointDispatcher != null)
                                {
                                    address = endpointDispatcher.EndpointAddress;
                                }
                                ServiceEndpoint exportedEndpoint = new ServiceEndpoint(endpoint.Contract);
                                exportedEndpoint.Binding = endpoint.Binding;
                                exportedEndpoint.Name = endpoint.Name;
                                exportedEndpoint.Address = address;
                                foreach (IEndpointBehavior behavior in endpoint.EndpointBehaviors)
                                {
                                    exportedEndpoint.EndpointBehaviors.Add(behavior);
                                }
                                exportedEndpoints.Add(exportedEndpoint);
                            }
                        }
                        WsdlExporter wsdlExporter = exporter as WsdlExporter;
                        if (wsdlExporter != null)
                        {
                            // Pass the BindingParameterCollection into the ExportEndpoints method so that the binding parameters can be using to export WSDL correctly.
                            // The binding parameters are used in BuildChannelListener, during which they can modify the configuration of the channel in ways that might have to
                            // be communicated in the WSDL. For example, in the case of Multi-Auth, the AuthenticationSchemesBindingParameter is used during BuildChannelListener
                            // to set the AuthenticationSchemes supported by the virtual directory on the HttpTransportBindingElement.  These authentication schemes also need
                            // to be in the WSDL, so that clients know what authentication schemes are supported by the service.
                            Fx.Assert(host != null, "ServiceHostBase field on MetadataExtensionInitializer should never be null.");
                            wsdlExporter.ExportEndpoints(exportedEndpoints, serviceName, GetBindingParameters(host, exportedEndpoints));
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
                        metadataGenerationException = e;
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
                    Security.ChannelProtectionRequirements requirements = parameters.Find<Security.ChannelProtectionRequirements>();
                    if (requirements == null)
                    {
                        requirements = new Security.ChannelProtectionRequirements();
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

            private static void TraceWsdlExportErrors(MetadataExporter exporter)
            {
                //foreach (MetadataConversionError error in exporter.Errors)
                //{
                //    if (DiagnosticUtility.ShouldTraceWarning)
                //    {
                //        Hashtable h = new Hashtable(2)
                //        {
                //            { "IsWarning", error.IsWarning },
                //            { "Message", error.Message }
                //        };
                //        TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.WsmexNonCriticalWsdlExportError,
                //            SR.GetString(SR.TraceCodeWsmexNonCriticalWsdlExportError), new DictionaryTraceRecord(h), null, null);
                //    }
                //}
            }
        }

    }
}
