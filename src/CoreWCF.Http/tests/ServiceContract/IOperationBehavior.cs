using System;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace ServiceContract
{
    [ServiceContract]
    public interface IOperationBehaviorBasic_ByHand
    {
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    public interface IOperationBehaviorBasic_CustomAttribute
    {
        [MyOperationBehavior]
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    public interface IOperationBehaviorBasic_TwoAttributesDifferentTypes
    {
        [MyOperationBehavior]
        [MyOtherOperationBehavior]
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    public interface IOperationBehaviorBasic_TwoAttributesSameType
    {
        [MyOperationBehavior]
        [MyOperationBehavior]
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    public interface IOperationBehaviorBasic_CustomAttributesImplementsOther
    {
        [MyMultiFacetedBehaviorAttribute]
        [OperationContract]
        string StringMethod(string s);
    }

    [ServiceContract]
    [MyOperationBehavior]
    public interface IOperationBehaviorBasic_MisplacedAttributes
    {
        [OperationContract]
        string StringMethod([MyOperationBehavior] string s);
    }

    [ServiceContract]
    public interface IOperationBehaviorBasic_ForStarAction
    {
        [OperationContract(Action = "*")]
        string StringMethod(string s);
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class MyOperationBehaviorAttribute : CustomBehaviorAttribute, IOperationBehavior
    {
        public static bool callBackBindDispatchMade = false;
        public static bool callBackBindProxyMade = false;

        #region IOperationBehavior Members
        public void Validate(OperationDescription description)
        {
        }
        public void AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }
        public void ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
            callBackBindProxyMade = true;
            m_BehaviorFlags.ProxyOperationBehaviorFlag = true;
        }

        public void ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            callBackBindDispatchMade = true;
            m_BehaviorFlags.DisptacherOperationBehaviorFlag = true;
        }
        #endregion
    }

    public class CustomStarActionBehavior : CustomBehaviorAttribute, IOperationBehavior, IContractBehavior
    {
        DispatchOperation unhandledDispatchOperation = null;
        public bool isUnhandledDispatchOperationUsedInOperationBehavior = false;

        #region IOperationBehavior Members
        public void Validate(OperationDescription description)
        {
        }
        public void AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }
        public void ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {

        }

        public void ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            if (unhandledDispatchOperation == dispatch)
            {
                isUnhandledDispatchOperationUsedInOperationBehavior = true;
            }
        }
        #endregion

        #region IContractBehavior Members
        public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, DispatchRuntime dispatchRuntime)
        {
            unhandledDispatchOperation = dispatchRuntime.UnhandledDispatchOperation;
        }

        public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint)
        {
        }
        #endregion
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class MyOtherOperationBehaviorAttribute : CustomBehaviorAttribute, IOperationBehavior
    {
        public void Validate(OperationDescription description)
        {
        }
        public void AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }
        public void ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
            m_BehaviorFlags.ProxyOperationBehaviorFlag = true;
        }

        public void ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            m_BehaviorFlags.DisptacherOperationBehaviorFlag = true;
        }
    }
}
