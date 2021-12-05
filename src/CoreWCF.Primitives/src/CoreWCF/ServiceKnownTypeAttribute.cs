// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = true, AllowMultiple = true)]
    public sealed class ServiceKnownTypeAttribute : Attribute
    {
        internal ServiceKnownTypeAttribute()
        {
            // Disallow default constructor for outside assemblies.
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

        public Type DeclaringType { get; internal set; }

        public string MethodName { get; internal set; }

        public Type Type { get; internal set; }
    }
}
