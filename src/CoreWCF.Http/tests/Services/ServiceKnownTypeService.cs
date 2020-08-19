using CoreWCF;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Services
{
    public class ServiceKnownTypeService : ServiceContract.IServiceKnownTypeTest, ClientContract.IServiceKnownTypeTest
    {
        public ServiceContract.BaseHelloReply SayHello(ServiceContract.HelloRequest request)
        {
            return new ServiceContract.HelloReply
            {
                Name = request.Name,
                Message = "Hello " + request.Name
            };
        }

        public ClientContract.BaseHelloReply SayHello(ClientContract.HelloRequest request)
        {
            return new ClientContract.HelloReply
            {
                Name = request.Name,
                Message = "Hello " + request.Name
            };
        }
    }

    [ServiceKnownType("GetKnownTypes")]
    public class ServiceKnownTypeWithAttribute : ServiceKnownTypeService
    {
        public static IEnumerable<Type> GetKnownTypes(ICustomAttributeProvider provider)
        {
            return new List<Type> { typeof(ServiceContract.HelloReply) };
        }
    }

    public static class ServiceKnownTypeServiceHelper
    {
        public static IEnumerable<Type> GetKnownTypes(ICustomAttributeProvider provider)
        {
            return new List<Type> { typeof(ServiceContract.HelloReply) };
        }
    }
}
