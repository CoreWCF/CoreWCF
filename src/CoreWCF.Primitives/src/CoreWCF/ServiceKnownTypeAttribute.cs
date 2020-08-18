using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = true, AllowMultiple = true)]
    public sealed class ServiceKnownTypeAttribute : Attribute
    {
        private ServiceKnownTypeAttribute()
        {
            // Disallow default constructor
        }

        public ServiceKnownTypeAttribute(Type type)
        {
            Type = type;
        }

        public ServiceKnownTypeAttribute(string methodName)
        {
            MethodName = methodName;
        }

        public ServiceKnownTypeAttribute(string methodName, Type declaringType)
        {
            MethodName = methodName;
            DeclaringType = declaringType;
        }

        public Type DeclaringType { get; }

        public string MethodName { get; }

        public Type Type { get; }
    }
}