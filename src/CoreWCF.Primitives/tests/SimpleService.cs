// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

class SimpleService : ISimpleService
{
    public string Echo(string echo)
    {
        return echo;
    }
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
class SimpleSingletonService : ISimpleService
{
    public string Echo(string echo)
    {
        return echo;
    }
}
