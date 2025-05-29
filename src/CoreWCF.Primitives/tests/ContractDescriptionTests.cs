// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class ContractDescriptionTests
    {
        [Fact]
        public void ValidateContractCanBeConstructed()
        {
            ContractDescription.GetContract<MessagePropertyService>(typeof(IMessagePropertyService));
        }

        [Fact]
        public void ValidateContractCanBeConstructed_WhenUsingInheritance()
        {
            ContractDescription.GetContract<WhenUsingInheritance.ServiceV2>(typeof(WhenUsingInheritance.IServiceV2));
        }

        public class MessagePropertyService : IMessagePropertyService
        {
            public SimpleResponse Request(SimpleRequest request)
            {
                return new SimpleResponse
                {
                    stringParam = request.stringParam,
                    stringProperty = request.stringParam
                };
            }
        }

        [System.ServiceModel.ServiceContract]
        internal interface IMessagePropertyService
        {
            [System.ServiceModel.OperationContract]
            SimpleResponse Request(SimpleRequest request);
        }

        [DataContract]
        public class SimpleRequest : BaseRequest
        {
        }

        [System.ServiceModel.MessageContract(WrapperName = "MPRequest", WrapperNamespace = "http://tempuri.org")]
        public class BaseRequest
        {
            [System.ServiceModel.MessageBodyMember]
            public string stringParam;
        }

        [DataContract]
        public class SimpleResponse : BaseResponse
        {
        }

        [System.ServiceModel.MessageContract(WrapperName = "MPResponse", WrapperNamespace = "http://tempuri.org")]
        public abstract class BaseResponse
        {
            public const string PropertyName = "MPMessageProperty";
            [System.ServiceModel.MessageBodyMember]
            public string stringParam;
            [System.ServiceModel.MessageProperty(Name = PropertyName)]
            public string stringProperty;
        }
    }

    internal class WhenUsingInheritance
    {
        [ServiceContract]
        public interface IService
        {
            [OperationContract]
            void Operation1();
        }

        public class Service : IService
        {
            [AuthorizeRole("RoleName")]
            public void Operation1()
            {
            }
        }

        [ServiceContract]
        public interface IServiceV2 : IService
        {
        }

        public class ServiceV2 : Service, IServiceV2
        {
        }
    }
}
