using CoreWCF;
using System;
using System.Collections.Generic;
using System.Text;

[System.ServiceModel.ServiceContract]
[ServiceContract]
interface ISimpleService
{
    [System.ServiceModel.OperationContract]
    [OperationContract]
    string Echo(string echo);
}
