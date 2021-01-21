// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

class SimpleService : ISimpleService
{
    public string Echo(string echo)
    {
        return echo;
    }
}
