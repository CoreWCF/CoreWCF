// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ServiceContract;

namespace Services
{
    public class Service_1144850 : SCInterfaceAB_1144850, SCInterfaceA_1144850, SCInterfaceB_1144850
    {
        public string StringMethodAB(string str)
        {
            return str;
        }

        public string StringMethodA(string str)
        {
            return str;
        }

        public string StringMethodB(string str)
        {
            return str;
        }
    }
}
