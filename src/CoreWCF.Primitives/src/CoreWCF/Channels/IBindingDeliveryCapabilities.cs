// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    // Bindings can implement this interface in order to advertise capabilities 
    // that DeliveryRequirementsAttribute consumes
    public interface IBindingDeliveryCapabilities
    {
        // This bool gives an assurance: "true" means you get Ordered, "false" means you may not
        // [DeliveryRequirements(RequireOrderedDelivery)] consumes this
        bool AssuresOrderedDelivery { get; }

        // Is this binding a queue (in the transacted-receive sense)
        // [DeliveryRequirements(QueuedDeliveryRequirements)] consumes this
        bool QueuedDelivery { get; }
    }
}
