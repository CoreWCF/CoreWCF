// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public Type Type { get; }
    }
}