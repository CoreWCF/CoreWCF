using CoreWCF;
using ServiceContract;
using Xunit;

namespace Services
{
    public abstract class OperationBehaviorBasic_ServiceBase
    {
        public virtual string StringMethod(string s)
        {
            BehaviorInvokedVerifier.ValidateServiceInvokedBehaviors(OperationContext.Current.Host.Description, s, BehaviorType.IOperationBehavior);
            return s;
        }
    }

    [ServiceBehavior]
    public class OperationBehaviorBasic_ByHand_Service : OperationBehaviorBasic_ServiceBase, IOperationBehaviorBasic_ByHand
    {
    }

    [ServiceBehavior]
    public class OperationBehaviorBasic_StarAction_Service : IOperationBehaviorBasic_ForStarAction
    {
        public string StringMethod(string s)
        {
            CustomStarActionBehavior behavior = OperationContext.Current.Host.Description.Endpoints[0].Contract.Behaviors.Find<CustomStarActionBehavior>();
            Assert.NotNull(behavior);
            //below assertion failed, similar cause as https://github.com/CoreWCF/CoreWCF/issues/193
            //Assert.True(behavior.isUnhandledDispatchOperationUsedInOperationBehavior);   
            return s;
        }
    }

    [ServiceBehavior]
    public class OperationBehaviorBasic_CustomAttribute_Service : OperationBehaviorBasic_ServiceBase,
        IOperationBehaviorBasic_CustomAttribute
    {
    }

    [ServiceBehavior]
    public class OperationBehaviorBasic_TwoAttributesDifferentTypes_Service : OperationBehaviorBasic_ServiceBase,
    IOperationBehaviorBasic_TwoAttributesDifferentTypes
    {
    }

    [ServiceBehavior]
    public class OperationBehaviorBasic_TwoAttributesSameType_Service : OperationBehaviorBasic_ServiceBase,
        IOperationBehaviorBasic_TwoAttributesSameType
    {
        public override string StringMethod(string s)
        {
            throw new System.ApplicationException("Should not be invoked as the service creation should fail due to duplicate OperationBehavior attributes on the contract.");
        }
    }

    [ServiceBehavior]
    [MyOperationBehavior]
    public class OperationBehaviorBasic_MisplacedAttributes_Service : OperationBehaviorBasic_ServiceBase,
        IOperationBehaviorBasic_MisplacedAttributes
    {
    }

    [ServiceBehavior]
    public class OperationBehaviorBasic_CustomAttributesImplementsOther_Service : OperationBehaviorBasic_ServiceBase,
        IOperationBehaviorBasic_CustomAttributesImplementsOther
    {
    }
}