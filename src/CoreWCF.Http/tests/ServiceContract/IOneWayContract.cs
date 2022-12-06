﻿using CoreWCF;
using System.Threading.Tasks;

namespace ServiceContract
{
    [ServiceContract]
    public interface IOneWayContract
    {
        // Token: 0x06000864 RID: 2148
        [OperationContract(AsyncPattern = true, IsOneWay = true)]
        Task OneWay(string s);
    }
}
