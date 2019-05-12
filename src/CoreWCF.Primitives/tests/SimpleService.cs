﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Primitives.Tests
{
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
}
