using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = true, AllowMultiple = true)]
    public sealed class ServiceKnownTypeAttribute : Attribute
    {
        //Type _declaringType;
        //string _methodName;
        readonly Type _type;

        private ServiceKnownTypeAttribute()
        {
            // Disallow default constructor
        }

        public ServiceKnownTypeAttribute(Type type)
        {
            _type = type;
        }

        // The named method must take a parameter of ICustomAttributeProvider which isn't available so this overload can't be used
        //public ServiceKnownTypeAttribute(string methodName)
        //{
        //    _methodName = methodName;
        //}

        //public ServiceKnownTypeAttribute(string methodName, Type declaringType)
        //{
        //    _methodName = methodName;
        //    _declaringType = declaringType;
        //}

        //public Type DeclaringType => _declaringType;

        //public string MethodName => _methodName;

        public Type Type => _type;
    }
}