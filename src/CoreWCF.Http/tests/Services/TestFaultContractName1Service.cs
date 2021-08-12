// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class FaultContract_859456_Service1 : ITestFaultContractName1
    {
        #region TwoWay_Methods
        public string Method1(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }
            else
            {
                System.Console.WriteLine("Error, Received Input: {0}", s);
            }
            return s;
        }
        #endregion
    }
}
