using System;
using System.Collections.Generic;
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
}
