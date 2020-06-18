using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace ClientContract
{
    [ServiceContract]
    public interface IContractBehaviorBasic_ByHand
    {
        [OperationContract]
        string StringMethod(string s);
    }

    [CustomContractBehavior]
    [ServiceContract]
    public interface IContractBehaviorBasic_CustomAttribute
    {
        [OperationContract]
        string StringMethod(string s);
    }

    [CustomContractBehavior]
    [OtherCustomContractBehavior]
    [ServiceContract]
    public interface IContractBehaviorBasic_TwoAttributesDifferentTypes
    {
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    [CustomContractBehavior]
    [CustomContractBehavior]
    public interface IContractBehaviorBasic_TwoAttributesSameType
    {
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    [MyMultiFacetedBehavior]
    public interface IContractBehaviorBasic_CustomAttributesImplementsOther
    {
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    public interface IContractBehaviorBasic_MisplacedAttributes
    {
        [OperationContract]
        [MisplacedCustomContractBehavior]
        string StringMethod([MisplacedCustomContractBehavior] string s);
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class CustomContractBehaviorAttribute : CustomBehaviorAttribute, IContractBehavior
    {
        public virtual void ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, DispatchRuntime dispatch)
        {
            m_BehaviorFlags.DisptacherContractBehaviorFlag = true;
        }

        public virtual void Validate(ContractDescription description, ServiceEndpoint endpoint)
        {
        } 
        
        public virtual void AddBindingParameters(ContractDescription description, ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }  
        
        public virtual void ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, ClientRuntime proxy)
        {
            m_BehaviorFlags.ProxyContractBehaviorFlag = true;
        }
    }

    public class OtherCustomContractBehaviorAttribute : CustomBehaviorAttribute, IContractBehavior
    {
        public virtual void ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, DispatchRuntime dispatch)
        {
            m_BehaviorFlags.DisptacherContractBehaviorFlag = true;
        }

        public virtual void Validate(ContractDescription description, ServiceEndpoint endpoint)
        {
        }

        public virtual void AddBindingParameters(ContractDescription description, ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }

        public virtual void ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, ClientRuntime proxy)
        {
            m_BehaviorFlags.ProxyContractBehaviorFlag = true;
        }
    }

    public class MisplacedCustomContractBehaviorAttribute : CustomBehaviorAttribute, IContractBehavior
    {
        public virtual void ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, DispatchRuntime dispatch)
        {
            m_BehaviorFlags.DisptacherContractBehaviorFlag = true;
        }

        public virtual void Validate(ContractDescription description, ServiceEndpoint endpoint)
        {
        }
        public virtual void AddBindingParameters(ContractDescription description, ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }

        public virtual void ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, ClientRuntime proxy)
        {
            m_BehaviorFlags.ProxyContractBehaviorFlag = true;
        }
    }
}
