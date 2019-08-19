using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Security;

namespace CoreWCF.Description
{
    internal class DispatcherBuilder
    {
        private static void ValidateDescription(ServiceDescription description, ServiceHostBase serviceHost)
        {
            description.EnsureInvariants();
            //(SecurityValidationBehavior.Instance as IServiceBehavior).Validate(description, serviceHost);
            (new UniqueContractNameValidationBehavior() as IServiceBehavior).Validate(description, serviceHost);
            for (int i = 0; i < description.Behaviors.Count; i++)
            {
                IServiceBehavior iServiceBehavior = description.Behaviors[i];
                iServiceBehavior.Validate(description, serviceHost);
            }
            for (int i = 0; i < description.Endpoints.Count; i++)
            {
                ServiceEndpoint endpoint = description.Endpoints[i];
                ContractDescription contract = endpoint.Contract;
                bool alreadyProcessedThisContract = false;
                for (int j = 0; j < i; j++)
                {
                    if (description.Endpoints[j].Contract == contract)
                    {
                        alreadyProcessedThisContract = true;
                        break;
                    }
                }
                endpoint.ValidateForService(!alreadyProcessedThisContract);
            }
        }

        static void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
            foreach (IContractBehavior icb in endpoint.Contract.Behaviors)
            {
                icb.AddBindingParameters(endpoint.Contract, endpoint, parameters);
            }
            foreach (IEndpointBehavior ieb in endpoint.Behaviors)
            {
                ieb.AddBindingParameters(endpoint, parameters);
            }
            foreach (OperationDescription op in endpoint.Contract.Operations)
            {
                foreach (IOperationBehavior iob in op.Behaviors)
                {
                    iob.AddBindingParameters(op, parameters);
                }
            }
        }

        private static void EnsureThereAreApplicationEndpoints(ServiceDescription description)
        {
            foreach (ServiceEndpoint endpoint in description.Endpoints)
            {
                if (!endpoint.InternalIsSystemEndpoint(description))
                {
                    return;
                }
            }
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                                                          SR.Format(SR.ServiceHasZeroAppEndpoints, description.ConfigurationName)));
        }

        internal static Uri EnsureListenUri(ServiceHostBase serviceHost, ServiceEndpoint endpoint)
        {
            Uri listenUri = endpoint.ListenUri;
            if (listenUri == null)
            {
                // TODO: Make sure the InternalBaseAddresses are populated with the relevant base address for the transport via DI
                listenUri = GetVia(endpoint.Binding.Scheme, ServiceHostBase.EmptyUri, serviceHost.InternalBaseAddresses);
            }
            if (listenUri == null)
            {
                // TODO: Plumb through expected scheme and update exception message
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxEndpointNoMatchingScheme, endpoint.Binding.Scheme, endpoint.Binding.Name, "")));
            }
            return listenUri;
        }

        internal static Uri GetVia(string scheme, Uri address, UriSchemeKeyedCollection baseAddresses)
        {
            Uri via = address;
            if (!via.IsAbsoluteUri)
            {
                if (!baseAddresses.Contains(scheme))
                {
                    return null;
                }

                via = GetUri(baseAddresses[scheme], address.OriginalString);
            }
            return via;
        }

        internal static Uri GetUri(Uri baseUri, string path)
        {
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
            {
                int i = 1;
                for (; i < path.Length; ++i)
                {
                    if (path[i] != '/' && path[i] != '\\')
                    {
                        break;
                    }
                }
                path = path.Substring(i);
            }

            // VSWhidbey#541152: new Uri(Uri, string.Empty) is broken
            if (path.Length == 0)
                return baseUri;

            if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/");
            }
            return new Uri(baseUri, path);
        }

        internal static ListenUriInfo GetListenUriInfoForEndpoint(ServiceHostBase host, ServiceEndpoint endpoint)
        {
            Uri listenUri = EnsureListenUri(host, endpoint);
            return new ListenUriInfo(listenUri, endpoint.ListenUriMode);
        }

        internal static void InitializeServiceHost(ServiceDescription description, ServiceHostBase serviceHost)
        {
            if (serviceHost.ImplementedContracts != null && serviceHost.ImplementedContracts.Count > 0)
            {
                EnsureThereAreApplicationEndpoints(description);
            }
            ValidateDescription(description, serviceHost);

            Dictionary<ListenUriInfo, StuffPerListenUriInfo> stuffPerListenUriInfo
                = new Dictionary<ListenUriInfo, StuffPerListenUriInfo>();
            Dictionary<EndpointAddress, Collection<EndpointInfo>> endpointInfosPerEndpointAddress
                = new Dictionary<EndpointAddress, Collection<EndpointInfo>>();

            // Ensure ListenUri and group endpoints per ListenUri
            for (int i = 0; i < description.Endpoints.Count; i++)
            {
                ServiceEndpoint endpoint = description.Endpoints[i];

                ListenUriInfo listenUriInfo = GetListenUriInfoForEndpoint(serviceHost, endpoint);
                if (!stuffPerListenUriInfo.ContainsKey(listenUriInfo))
                {
                    stuffPerListenUriInfo.Add(listenUriInfo, new StuffPerListenUriInfo());
                }
                stuffPerListenUriInfo[listenUriInfo].Endpoints.Add(endpoint);
            }

            foreach (KeyValuePair<ListenUriInfo, StuffPerListenUriInfo> stuff in stuffPerListenUriInfo)
            {
                Uri listenUri = stuff.Key.ListenUri;
                BindingParameterCollection parameters = stuff.Value.Parameters;
                Binding binding = stuff.Value.Endpoints[0].Binding;
                EndpointIdentity identity = stuff.Value.Endpoints[0].Address.Identity;
                // same EndpointAddressTable instance must be shared between channelDispatcher and parameters
                //ThreadSafeMessageFilterTable<EndpointAddress> endpointAddressTable = new ThreadSafeMessageFilterTable<EndpointAddress>();
                //parameters.Add(endpointAddressTable);

                // add service-level binding parameters
                foreach (IServiceBehavior behavior in description.Behaviors)
                {
                    behavior.AddBindingParameters(description, serviceHost, stuff.Value.Endpoints, parameters);
                }
                for (int i = 0; i < stuff.Value.Endpoints.Count; i++)
                {
                    ServiceEndpoint endpoint = stuff.Value.Endpoints[i];
                    string viaString = listenUri.AbsoluteUri;

                    // ensure all endpoints with this ListenUriInfo have same binding
                    if (endpoint.Binding != binding)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ABindingInstanceHasAlreadyBeenAssociatedTo1, viaString)));
                    }

                    // ensure all endpoints with this ListenUriInfo have same identity
                    if (!object.Equals(endpoint.Address.Identity, identity))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                                                                      SR.Format(SR.SFxWhenMultipleEndpointsShareAListenUriTheyMustHaveSameIdentity, viaString)));
                    }

                    // add binding parameters (endpoint scope and below)
                    AddBindingParametersForSecurityContractInformation(endpoint, parameters);
                    AddBindingParameters(endpoint, parameters);
                }

                XmlQualifiedName bindingQname = new XmlQualifiedName(binding.Name, binding.Namespace);
                ChannelDispatcher channelDispatcher = new ChannelDispatcher(listenUri, binding, bindingQname.ToString(), binding);
                //channelDispatcher.SetEndpointAddressTable(endpointAddressTable);
                stuff.Value.ChannelDispatcher = channelDispatcher;

                for (int i = 0; i < stuff.Value.Endpoints.Count; i++)
                {
                    ServiceEndpoint endpoint = stuff.Value.Endpoints[i];
                    string viaString = listenUri.AbsoluteUri;

                    //EndpointFilterProvider provider = new EndpointFilterProvider();
                    EndpointDispatcher dispatcher = BuildEndpointDispatcher(description, endpoint);

                    if (!endpointInfosPerEndpointAddress.ContainsKey(endpoint.Address))
                    {
                        endpointInfosPerEndpointAddress.Add(endpoint.Address, new Collection<EndpointInfo>());
                    }
                    endpointInfosPerEndpointAddress[endpoint.Address].Add(new EndpointInfo(endpoint, dispatcher, /*provider*/ null));

                    channelDispatcher.Endpoints.Add(dispatcher);

                } // end foreach "endpoint"

                serviceHost.ChannelDispatchers.Add(channelDispatcher);
            } // end foreach "ListenUri/ChannelDispatcher" group

            // run service behaviors
            for (int i = 0; i < description.Behaviors.Count; i++)
            {
                IServiceBehavior serviceBehavior = description.Behaviors[i];
                serviceBehavior.ApplyDispatchBehavior(description, serviceHost);
            }

            foreach (KeyValuePair<ListenUriInfo, StuffPerListenUriInfo> stuff in stuffPerListenUriInfo)
            {
                for (int i = 0; i < stuff.Value.Endpoints.Count; i++)
                {
                    ServiceEndpoint endpoint = stuff.Value.Endpoints[i];
                    // rediscover which dispatcher goes with this endpoint
                    Collection<EndpointInfo> infos = endpointInfosPerEndpointAddress[endpoint.Address];
                    EndpointInfo info = null;
                    foreach (EndpointInfo ei in infos)
                    {
                        if (ei.Endpoint == endpoint)
                        {
                            info = ei;
                            break;
                        }
                    }
                    EndpointDispatcher dispatcher = info.EndpointDispatcher;
                    // run contract behaviors
                    for (int k = 0; k < endpoint.Contract.Behaviors.Count; k++)
                    {
                        IContractBehavior behavior = endpoint.Contract.Behaviors[k];
                        behavior.ApplyDispatchBehavior(endpoint.Contract, endpoint, dispatcher.DispatchRuntime);
                    }
                    // run endpoint behaviors
                    ApplyBindingInformationFromEndpointToDispatcher(endpoint, dispatcher);
                    for (int j = 0; j < endpoint.Behaviors.Count; j++)
                    {
                        IEndpointBehavior eb = endpoint.Behaviors[j];
                        eb.ApplyDispatchBehavior(endpoint, dispatcher);
                    }
                    // run operation behaviors
                    BindOperations(endpoint.Contract, null, dispatcher.DispatchRuntime);
                }
            }

            EnsureRequiredRuntimeProperties(endpointInfosPerEndpointAddress);

            // Warn about obvious demux conflicts
            foreach (Collection<EndpointInfo> endpointInfos in endpointInfosPerEndpointAddress.Values)
            {
                // all elements of endpointInfos share the same Address (and thus EndpointListener.AddressFilter)
                if (endpointInfos.Count > 1)
                {
                    for (int i = 0; i < endpointInfos.Count; i++)
                    {
                        for (int j = i + 1; j < endpointInfos.Count; j++)
                        {
                            // if not same ListenUri, won't conflict
                            // if not same ChannelType, may not conflict (some transports demux based on this)
                            // if they share a ChannelDispatcher, this means same ListenUri and same ChannelType
                            if (endpointInfos[i].EndpointDispatcher.ChannelDispatcher ==
                                endpointInfos[j].EndpointDispatcher.ChannelDispatcher)
                            {
                                EndpointFilterProvider iProvider = endpointInfos[i].FilterProvider;
                                EndpointFilterProvider jProvider = endpointInfos[j].FilterProvider;
                                // if not default EndpointFilterProvider, we won't try to throw, you're on your own
                                string commonAction;
                                if (iProvider != null && jProvider != null
                                    && HaveCommonInitiatingActions(iProvider, jProvider, out commonAction))
                                {
                                    // you will definitely get a MultipleFiltersMatchedException at runtime,
                                    // so let's go ahead and throw now
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                        new InvalidOperationException(
                                            SR.Format(SR.SFxDuplicateInitiatingActionAtSameVia,endpointInfos[i].Endpoint.ListenUri, commonAction)));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void EnsureRequiredRuntimeProperties(Dictionary<EndpointAddress, Collection<EndpointInfo>> endpointInfosPerEndpointAddress)
        {
            foreach (Collection<EndpointInfo> endpointInfos in endpointInfosPerEndpointAddress.Values)
            {
                for (int i = 0; i < endpointInfos.Count; i++)
                {
                    DispatchRuntime dispatch = endpointInfos[i].EndpointDispatcher.DispatchRuntime;

                    if (dispatch.InstanceContextProvider == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxRequiredRuntimePropertyMissing, "InstanceContextProvider")));
                    }
                }
            }
        }

        internal static EndpointDispatcher BuildEndpointDispatcher(ServiceDescription serviceDescription,
                                                  ServiceEndpoint endpoint)
        {
            if (serviceDescription == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceDescription));
            }

            var contractDescription = endpoint.Contract;
            if (contractDescription == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoint.Contract));
            }

            EndpointFilterProvider provider = new EndpointFilterProvider();

            EndpointAddress address = endpoint.Address;
            EndpointDispatcher dispatcher = new EndpointDispatcher(address, contractDescription.Name, contractDescription.Namespace, endpoint.Id, endpoint.InternalIsSystemEndpoint(serviceDescription));

            DispatchRuntime dispatch = dispatcher.DispatchRuntime;
            if (contractDescription.CallbackContractType != null)
            {
                dispatch.CallbackClientRuntime.CallbackClientType = contractDescription.CallbackContractType;
                dispatch.CallbackClientRuntime.ContractClientType = contractDescription.ContractType;
            }

            for (int i = 0; i < contractDescription.Operations.Count; i++)
            {
                OperationDescription operation = contractDescription.Operations[i];

                if (!operation.IsServerInitiated())
                {
                    BuildDispatchOperation(operation, dispatch, provider);
                }
                else
                {
                    BuildProxyOperation(operation, dispatch.CallbackClientRuntime);
                }
            }

            BindOperations(contractDescription, null, dispatch);

            //dispatcher.SetSupportedChannels(DispatcherBuilder.GetSupportedChannelTypes(contractDescription));
            int filterPriority = 0;
            dispatcher.ContractFilter = provider.CreateFilter(out filterPriority);
            dispatcher.FilterPriority = filterPriority;

            return dispatcher;
        }

        static void BuildProxyOperation(OperationDescription operation, ClientRuntime parent)
        {
            ClientOperation child;
            if (operation.Messages.Count == 1)
            {
                child = new ClientOperation(parent, operation.Name, operation.Messages[0].Action);
            }
            else
            {
                child = new ClientOperation(parent, operation.Name, operation.Messages[0].Action,
                                            operation.Messages[1].Action);
            }
            child.TaskMethod = operation.TaskMethod;
            child.TaskTResult = operation.TaskTResult;
            child.SyncMethod = operation.SyncMethod;
            child.BeginMethod = operation.BeginMethod;
            child.EndMethod = operation.EndMethod;
            child.IsOneWay = operation.IsOneWay;
            child.IsTerminating = operation.IsTerminating;
            child.IsInitiating = operation.IsInitiating;
            child.IsSessionOpenNotificationEnabled = operation.IsSessionOpenNotificationEnabled;
            for (int i = 0; i < operation.Faults.Count; i++)
            {
                FaultDescription fault = operation.Faults[i];
                child.FaultContractInfos.Add(new FaultContractInfo(fault.Action, fault.DetailType, fault.ElementName, fault.Namespace, operation.KnownTypes));
            }

            parent.Operations.Add(child);
        }

        static void BuildDispatchOperation(OperationDescription operation, DispatchRuntime parent, EndpointFilterProvider provider)
        {
            string requestAction = operation.Messages[0].Action;
            DispatchOperation child = null;
            if (operation.IsOneWay)
            {
                child = new DispatchOperation(parent, operation.Name, requestAction);
            }
            else
            {
                string replyAction = operation.Messages[1].Action;
                child = new DispatchOperation(parent, operation.Name, requestAction, replyAction);
            }

            child.HasNoDisposableParameters = operation.HasNoDisposableParameters;

            child.IsTerminating = operation.IsTerminating;
            child.IsSessionOpenNotificationEnabled = operation.IsSessionOpenNotificationEnabled;
            for (int i = 0; i < operation.Faults.Count; i++)
            {
                FaultDescription fault = operation.Faults[i];
                child.FaultContractInfos.Add(new FaultContractInfo(fault.Action, fault.DetailType, fault.ElementName, fault.Namespace, operation.KnownTypes));
            }

            if (provider != null)
            {
                if (operation.IsInitiating)
                {
                    provider.InitiatingActions.Add(requestAction);
                }
            }

            if (requestAction != MessageHeaders.WildcardAction)
            {
                parent.Operations.Add(child);
            }
            else
            {
                if (parent.HasMatchAllOperation)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxMultipleContractStarOperations0));
                }

                parent.UnhandledDispatchOperation = child;
            }

        }

        static void BindOperations(ContractDescription contract, ClientRuntime proxy, DispatchRuntime dispatch)
        {
            if (!(((proxy == null) != (dispatch == null))))
            {
                throw Fx.AssertAndThrowFatal("DispatcherBuilder.BindOperations: ((proxy == null) != (dispatch == null))");
            }

            MessageDirection local = (proxy == null) ? MessageDirection.Input : MessageDirection.Output;

            for (int i = 0; i < contract.Operations.Count; i++)
            {
                OperationDescription operation = contract.Operations[i];
                MessageDescription first = operation.Messages[0];

                if (first.Direction != local)
                {
                    if (proxy == null)
                    {
                        proxy = dispatch.CallbackClientRuntime;
                    }

                    ClientOperation proxyOperation = proxy.Operations[operation.Name];
                    Fx.Assert(proxyOperation != null, "");

                    for (int j = 0; j < operation.Behaviors.Count; j++)
                    {
                        IOperationBehavior behavior = operation.Behaviors[j];
                        behavior.ApplyClientBehavior(operation, proxyOperation);
                    }
                }
                else
                {
                    if (dispatch == null)
                    {
                        dispatch = proxy.CallbackDispatchRuntime;
                    }

                    DispatchOperation dispatchOperation = null;
                    if (dispatch.Operations.Contains(operation.Name))
                    {
                        dispatchOperation = dispatch.Operations[operation.Name];
                    }
                    if (dispatchOperation == null && dispatch.UnhandledDispatchOperation != null && dispatch.UnhandledDispatchOperation.Name == operation.Name)
                    {
                        dispatchOperation = dispatch.UnhandledDispatchOperation;
                    }

                    if (dispatchOperation != null)
                    {
                        for (int j = 0; j < operation.Behaviors.Count; j++)
                        {
                            IOperationBehavior behavior = operation.Behaviors[j];
                            behavior.ApplyDispatchBehavior(operation, dispatchOperation);
                        }
                    }
                }
            }
        }

        static bool HaveCommonInitiatingActions(EndpointFilterProvider x, EndpointFilterProvider y, out string commonAction)
        {
            commonAction = null;
            foreach (string action in x.InitiatingActions)
            {
                if (y.InitiatingActions.Contains(action))
                {
                    commonAction = action;
                    return true;
                }
            }
            return false;
        }

        internal static List<IServiceDispatcher> BuildDispatcher<TService>(ServiceConfiguration<TService> serviceConfig, IServiceProvider services) where TService : class
        {
            var serviceBuilder = services.GetRequiredService<IServiceBuilder>();
            var serverUriAddresses = serviceBuilder.BaseAddresses.ToArray();
            ServiceHostObjectModel < TService > serviceHost;
            TService singletonInstance = services.GetService<TService>();
            if (singletonInstance == null)
            {
                serviceHost = new ServiceHostObjectModel<TService>(serverUriAddresses);
            }
            else
            {
                serviceHost = new ServiceHostObjectModel<TService>(singletonInstance, serverUriAddresses);
            }

            // Any user supplied IServiceBehaviors can be applied now
            var serviceBehaviors = services.GetServices<IServiceBehavior>();
            foreach (var behavior in serviceBehaviors)
            {
                serviceHost.Description.Behaviors.Add(behavior);
            }

            // TODO: Create internal behavior which configures any extensibilities which exist in serviceProvider, eg IMessageInspector
            foreach (var endpointConfig in serviceConfig.Endpoints)
            {
                if (!serviceHost.ReflectedContracts.Contains(endpointConfig.Contract))
                {
                    throw new ArgumentException($"Service type {typeof(TService)} doesn't implement interface {endpointConfig.Contract}");
                }

                ContractDescription contract = serviceHost.ReflectedContracts[endpointConfig.Contract];
                var uri = serviceHost.MakeAbsoluteUri(endpointConfig.Address, endpointConfig.Binding);
                var serviceEndpoint = new ServiceEndpoint(
                    contract,
                    endpointConfig.Binding,
                    new EndpointAddress(uri));

                serviceHost.Description.Endpoints.Add(serviceEndpoint);
            }

            InitializeServiceHost(serviceHost.Description, serviceHost);

            // TODO: Add error checking to make sure property chain is correctly populated with objects
            var dispatchers = new List<IServiceDispatcher>(serviceHost.ChannelDispatchers.Count);
            foreach (var cdb in serviceHost.ChannelDispatchers)
            {
                var cd = cdb as ChannelDispatcher;
                cd.Init();
                var openTask = cd.OpenAsync();
                Fx.Assert(openTask.IsCompleted, "ChannelDispatcher should open synchronously");
                openTask.GetAwaiter().GetResult();
                dispatchers.Add(new ServiceDispatcher(cd));
            }

            return dispatchers;
        }

        public static void ApplyBindingInformationFromEndpointToDispatcher(ServiceEndpoint serviceEndpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.ChannelDispatcher.ReceiveSynchronously = false; // No sync code for .Net Core
            endpointDispatcher.ChannelDispatcher.ManualAddressing = IsManualAddressing(serviceEndpoint.Binding);
            endpointDispatcher.ChannelDispatcher.EnableFaults = true;
            endpointDispatcher.ChannelDispatcher.MessageVersion = serviceEndpoint.Binding.MessageVersion;
        }

        internal static bool IsManualAddressing(Binding binding)
        {
            TransportBindingElement transport = binding.CreateBindingElements().Find<TransportBindingElement>();
            if (transport == null)
            {
                string text = SR.Format(SR.SFxBindingMustContainTransport2, binding.Name, binding.Namespace);
                Exception error = new InvalidOperationException(text);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }
            return transport.ManualAddressing;
        }

        public static void AddBindingParametersForSecurityContractInformation(ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
            // get Contract info security needs, and put in BindingParameterCollection
            ISecurityCapabilities isc = null;
            BindingElementCollection elements = endpoint.Binding.CreateBindingElements();
            for (int i = 0; i < elements.Count; ++i)
            {
                if (!(elements[i] is ITransportTokenAssertionProvider))
                {
                    ISecurityCapabilities tmp = elements[i].GetIndividualProperty<ISecurityCapabilities>();
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

        #region InnerClasses
        class EndpointInfo
        {
            ServiceEndpoint endpoint;
            EndpointDispatcher endpointDispatcher;
            EndpointFilterProvider provider;

            public EndpointInfo(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher, EndpointFilterProvider provider)
            {
                this.endpoint = endpoint;
                this.endpointDispatcher = endpointDispatcher;
                this.provider = provider;
            }
            public ServiceEndpoint Endpoint { get { return endpoint; } }
            public EndpointFilterProvider FilterProvider { get { return provider; } }
            public EndpointDispatcher EndpointDispatcher { get { return endpointDispatcher; } }
        }

        internal class ListenUriInfo
        {
            Uri listenUri;
            ListenUriMode listenUriMode;

            public ListenUriInfo(Uri listenUri, ListenUriMode listenUriMode)
            {
                this.listenUri = listenUri;
                this.listenUriMode = listenUriMode;
            }

            public Uri ListenUri
            {
                get { return listenUri; }
            }

            public ListenUriMode ListenUriMode
            {
                get { return listenUriMode; }
            }

            // implement Equals and GetHashCode so that we can use this as a key in a dictionary
            public override bool Equals(object obj)
            {
                return Equals(obj as ListenUriInfo);
            }

            public bool Equals(ListenUriInfo other)
            {
                if (other == null)
                {
                    return false;
                }

                if (object.ReferenceEquals(this, other))
                {
                    return true;
                }

                return (listenUriMode == other.listenUriMode)
                    && EndpointAddress.UriEquals(listenUri, other.listenUri, true /* ignoreCase */, true /* includeHost */);
            }

            public override int GetHashCode()
            {
                return EndpointAddress.UriGetHashCode(listenUri, true /* includeHost */);
            }
        }

        class StuffPerListenUriInfo
        {
            public BindingParameterCollection Parameters = new BindingParameterCollection();
            public Collection<ServiceEndpoint> Endpoints = new Collection<ServiceEndpoint>();
            public ChannelDispatcher ChannelDispatcher = null;
        }
        #endregion // InnerClasses
    }
}