using CoreWCF;
using System;
using System.Collections.Generic;
using System.Text;

[ServiceContract]
interface ISimpleService
{
    [OperationContract]
    string Echo(string echo);
}
