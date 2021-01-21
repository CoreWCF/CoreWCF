// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class SanityAParentB_857419_Service_Both : ISanityAParentB_857419_ContractBase, ISanityAParentB_857419_ContractDerived
    {
        #region IContractDerived Members

        public void OneWayMethod(object o)
        {
            string input = o as string;
            if (input == null)
            {
                throw new NullReferenceException("clientString was not in the SharedContext");
            }

            return;
        }

        public string StringMethod(string s)
        {
            Console.WriteLine(s);
            return s;
        }

        string ISanityAParentB_857419_ContractDerived.Method(string input)
        {
            if (input == null || input.ToLower().Contains("derived") == false)
            {
                throw new Exception("Wrong value received = <" + input + ">");
            }

            return input;
        }

        #endregion

        public string TwoWayMethod(string input)
        {
            if (input == null)
            {
                throw new NullReferenceException("clientString was not in the SharedContext");
            }

            return input;
        }

        public object DataContractMethod(object o)
        {
            MyBaseDataType data = o as MyBaseDataType;
            if (data == null)
            {
                throw new NullReferenceException("DataContractMethod received null " + o);
            }

            string input = data.data;
            if (input == null)
            {
                throw new NullReferenceException("clientString was not in the SharedContext");
            }

            return data;
        }

        public string Method(string input)
        {
            if (input == null || input.ToLower().Contains("base") == false)
            {
                throw new Exception("Wrong value received = <" + input + ">");
            }
            return input;
        }
    }
}

