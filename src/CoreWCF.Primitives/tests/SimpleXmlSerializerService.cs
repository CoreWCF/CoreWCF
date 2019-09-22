using System;
using System.Collections.Generic;
using System.Text;



namespace CoreWCF.Primitives.Tests
{
    class SimpleXmlSerializerService : ISimpleXmlSerializerService
    {
        public string Echo(string echo)
        {
            return echo;
        }
    }
}
