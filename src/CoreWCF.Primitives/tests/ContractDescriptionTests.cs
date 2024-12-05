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
        public void ValidateInheritedContractWithAuthorizeRoleAttributeCanBeConstructed()
        {
            ContractDescription.GetContract<MessagePropertyService2>(typeof(IMessagePropertyService));
        }

        private class MessagePropertyService2 : MessagePropertyService
        {
        }

        public class MessagePropertyService : IMessagePropertyService
        {
            [AuthorizeRole]
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
}
