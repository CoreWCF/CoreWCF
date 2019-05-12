using System;
using System.Collections.Generic;
using System.Text;

class SimpleService : ISimpleService
{
    public string Echo(string echo)
    {
        return echo;
    }
}
