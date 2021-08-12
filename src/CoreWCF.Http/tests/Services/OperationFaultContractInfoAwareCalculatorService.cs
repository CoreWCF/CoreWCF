// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using ServiceContract;

namespace Services
{
    public class OperationFaultContractInfoAwareCalculatorService : ICalculatorService
    {
        public int Divide(int a, int b) => a / b;
    }

    public class OperationFaultContractInfoAwareServiceBehavior : IServiceBehavior, IErrorHandler
    {
        public List<FaultContractInfo> FaultContractInfos { get; } = new List<FaultContractInfo>();

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {

        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ChannelDispatcher channelDispatcher in serviceHostBase.ChannelDispatchers)
            {
                foreach (var endpointDispatcher in channelDispatcher.Endpoints)
                {
                    foreach (var dispatchOperation in endpointDispatcher.DispatchRuntime.Operations)
                    {
                        foreach (var faultContractInfo in dispatchOperation.FaultContractInfos)
                        {
                            FaultContractInfos.Add(faultContractInfo);
                        }
                    }
                }
            }
        }

        public bool HandleError(Exception error) => true;

        public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
        {

        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {

        }
    }
}
