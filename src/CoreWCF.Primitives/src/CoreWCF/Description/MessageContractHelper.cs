// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace CoreWCF.Description
{
    // goal of this class to move logic from TypeLoader to build message contract
    internal static class MessageContractHelper
    {
        internal static bool IsMessageContract(Type type)
        {
            foreach (Attribute attr in type.GetCustomAttributes(inherit: false))
            {
                if (attr.GetType() == typeof(MessageContractAttribute)
                || (string.Compare(attr.GetType().FullName, ServiceReflector.SMMessageContractAttributeFullName, true) == 0))
                {
                    return true;
                }
            }
            return false;
        }

        private static HashSet<string> s_eligibleMessageList = new HashSet<string>()
            {
                ServiceReflector.CWCFMessageHeaderAttribute,
                ServiceReflector.CWCFMessageHeaderArrayAttribute,
                ServiceReflector.CWCFMessageBodyMemberAttribute,
                ServiceReflector.CWCFMessagePropertyAttribute,
                ServiceReflector.SMMessageHeaderAttributeFullName,
                ServiceReflector.SMMessageHeaderArrayAttributeFullName,
                ServiceReflector.SMMessageBodyMemberAttributeFullName,
                ServiceReflector.SMMessagePropertyAttributeFullName
            };

        internal static bool IsEligibleMember(MemberInfo memberInfo)
        {
            foreach (Attribute attr in memberInfo.GetCustomAttributes())
            {
                if (s_eligibleMessageList.Contains(attr.GetType().FullName))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsMessageHeader(MemberInfo memberInfo)
        {
            foreach (Attribute attr in memberInfo.GetCustomAttributes())
            {
                if ((attr.GetType() == typeof(MessageHeaderAttribute))
                    || (attr.GetType() == typeof(MessageHeaderArrayAttribute))
                    || (string.Compare(attr.GetType().FullName, ServiceReflector.SMMessageHeaderAttributeFullName, true) == 0)
                    || (string.Compare(attr.GetType().FullName, ServiceReflector.SMMessageHeaderArrayAttributeFullName, true) == 0)
                    )
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsMessageProperty(MemberInfo memberInfo)
        {
            foreach (Attribute attr in memberInfo.GetCustomAttributes())
            {
                if ((attr.GetType() == typeof(MessagePropertyAttribute))
                    || (string.Compare(attr.GetType().FullName, ServiceReflector.SMMessagePropertyAttributeFullName, true) == 0))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
