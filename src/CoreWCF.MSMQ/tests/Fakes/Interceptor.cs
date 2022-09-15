// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Queue;

namespace CoreWCF.MSMQ.Tests.Fakes
{
    public class Interceptor
    {
        public string Name { get; private set; }

        public void SetName(string name)
        {
            Name = name;
        }
    }
    /*
    internal class TestConnectionHandler : IQueueConnectionHandler
    {
        public int CallCount { get; private set; }

        public QueueMessageContext GetContext(PipeReader reader, string queueUrl)
        {
            CallCount++;
            return new QueueMessageContext();
        }
    }

    internal class TestServiceBuilder : IServiceBuilder
    {
        public CommunicationState State { get; }
        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort() => throw new NotImplementedException();

        public Task CloseAsync() => throw new NotImplementedException();

        public Task CloseAsync(CancellationToken token) => throw new NotImplementedException();

        public Task OpenAsync()
        {
            Opened?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task OpenAsync(CancellationToken token) => throw new NotImplementedException();

        public ICollection<Type> Services { get; }
        public ICollection<Uri> BaseAddresses { get; }
        public IServiceBuilder AddService<TService>() where TService : class => throw new NotImplementedException();

        public IServiceBuilder AddService<TService>(Action<ServiceOptions> options) where TService : class => throw new NotImplementedException();

        public IServiceBuilder AddService(Type service) => throw new NotImplementedException();

        public IServiceBuilder AddService(Type service, Action<ServiceOptions> options) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Action<ServiceEndpoint> configureEndpoint) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Action<ServiceEndpoint> configureEndpoint) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri) => throw new NotImplementedException();

        public IServiceBuilder AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri,
            Action<ServiceEndpoint> configureEndpoint) =>
            throw new NotImplementedException();
    }
    */
}
