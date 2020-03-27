﻿using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Extensibility
{
    public class TestServiceBehavior : IServiceBehavior
    {
        public IDispatchMessageInspector DispatchMessageInspector { get; set; }
        public TestInstanceProvider InstanceProvider { get; set; }
        public Func<IOperationInvoker, IOperationInvoker> OperationInvokerFactory { get; set; }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (var cdb in serviceHostBase.ChannelDispatchers)
            {
                var dispatcher = cdb as ChannelDispatcher;
                foreach (var endpointDispatcher in dispatcher.Endpoints)
                {
                    if (!endpointDispatcher.IsSystemEndpoint)
                    {
                        if (DispatchMessageInspector != null)
                        {
                            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(DispatchMessageInspector);
                        }

                        if (InstanceProvider != null)
                        {
                            endpointDispatcher.DispatchRuntime.InstanceProvider = InstanceProvider;
                        }
                    }
                }
            }

            foreach(var endpoint in serviceDescription.Endpoints)
            {
                foreach(var operation in endpoint.Contract.Operations)
                {
                    operation.OperationBehaviors.Add(new TestOperationBehavior(this));
                }
            }
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
    }

    internal class TestOperationBehavior : IOperationBehavior
    {
        private TestServiceBehavior _parent;

        public TestOperationBehavior(TestServiceBehavior testServiceBehavior)
        {
            _parent = testServiceBehavior;
        }

        public void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters) { }

        public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation) { }

        public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
        {
            if (_parent.OperationInvokerFactory != null)
            {
                dispatchOperation.Invoker = _parent.OperationInvokerFactory(dispatchOperation.Invoker);
            }
        }

        public void Validate(OperationDescription operationDescription) { }
    }

    internal class MyInvoker : IOperationInvoker
    {
        private IOperationInvoker _invoker;

        public MyInvoker(IOperationInvoker invoker)
        {
            _invoker = invoker;
        }

        public object[] AllocateInputs() => _invoker.AllocateInputs();

        public async ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
        {
            (object retval, object[] outputs) = await _invoker.InvokeAsync(instance, inputs);
            return (new Foo(), outputs);
        }

        public class Foo
        {
            public string Value { get; set; }
        }
    }
}
