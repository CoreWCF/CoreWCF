// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

internal class SimpleService : ISimpleService
{
    public string Echo(string echo)
    {
        return echo;
    }
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
internal class SimpleSingletonService : ISimpleService
{
    public string Echo(string echo)
    {
        return echo;
    }
}
