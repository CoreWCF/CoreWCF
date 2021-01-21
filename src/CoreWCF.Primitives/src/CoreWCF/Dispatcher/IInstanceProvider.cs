// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IInstanceProvider
    {
        object GetInstance(InstanceContext instanceContext);
        object GetInstance(InstanceContext instanceContext, Message message);
        void ReleaseInstance(InstanceContext instanceContext, object instance);
    }
}