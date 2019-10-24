﻿using CoreWCF;

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
