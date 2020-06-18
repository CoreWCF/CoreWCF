using CoreWCF;
using CoreWCF.Dispatcher;
using ServiceContract;

namespace Services
{
    public abstract class IContractBehaviorBasic_ServiceBase
    {
        public string ServiceContractMethod(string s)
        {
            //TODO: validate behavior
            //BehaviorInvokedVerifier.ValidateServiceInvokedBehaviors(OperationContext.Current.Host.Description);            
            return s;
        }
    }

    [ServiceBehavior]
    public class ContractBehaviorBasic_ByHand_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_ByHand
    {
        public string StringMethod(string s)
        {
            return ServiceContractMethod(s);
        }
    }

    [ServiceBehavior]
    public class ContractBehaviorBasic_ByHandImplementsOther_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_ByHand
    {
        public string StringMethod(string s)
        {
            return ServiceContractMethod(s);
        }
    }
    
    [ServiceBehavior]
    public class ContractBehaviorBasic_CustomAttribute_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_CustomAttribute
    {
        public string StringMethod(string s)
        {
            return ServiceContractMethod(s);
        }
    }

    [ServiceBehavior]
    public class ContractBehaviorBasic_TwoAttributesDifferentTypes_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_TwoAttributesDifferentTypes
    {
        public string StringMethod(string s)
        {
            return ServiceContractMethod(s);
        }
    }

    [ServiceBehavior]
    public class ContractBehaviorBasic_TwoAttributesSameType_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_TwoAttributesSameType
    {
        public string StringMethod(string s)
        {
            throw new System.ApplicationException("Should never be called. Should throw exception when setting up the service as it has more than one ContractBehavior of same type");
        }
    }

    [ServiceBehavior]
    public class ContractBehaviorBasic_MisplacedAttributes_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_MisplacedAttributes
    {
        public string StringMethod(string s)
        {
            return ServiceContractMethod(s);
        }
    }

    [ServiceBehavior]
    public class ContractBehaviorBasic_CustomAttributesImplementsOther_Service : IContractBehaviorBasic_ServiceBase,
        IContractBehaviorBasic_CustomAttributesImplementsOther
    {
        public string StringMethod(string s)
        {
            return ServiceContractMethod(s);
        }
    }
}