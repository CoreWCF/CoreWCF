// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class DuplexTestService : IDuplexTestService
    {
        private readonly IProducerConsumerCollection<IDuplexTestCallback> _registeredClients = new ConcurrentBag<IDuplexTestCallback>();

        public bool RegisterDuplexChannel()
        {
            var callback = OperationContext.Current.GetCallbackChannel<IDuplexTestCallback>();
            return _registeredClients.TryAdd(callback);
        }

        public void SendMessage(string message)
        {
            foreach (var client in _registeredClients)
            {
                client.AddMessage(message);
            }
        }
    }
}
