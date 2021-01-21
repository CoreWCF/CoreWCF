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
            foreach (Attribute attr in type.GetCustomAttributes())
            {
                if (attr.GetType() == typeof(MessageContractAttribute)
                || (String.Compare(attr.GetType().FullName, ServiceReflector.SMMessageContractAttributeFullName, true) == 0))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsEligibleMember(MemberInfo memberInfo)
        {
            HashSet<String> eligibleMessageList = new HashSet<string>()
            {
                 ServiceReflector.CWCFMesssageHeaderAttribute
                , ServiceReflector.CWCFMesssageHeaderArrayAttribute
                ,ServiceReflector.CWCFMesssageBodyMemberAttribute
                ,ServiceReflector.CWCFMesssagePropertyAttribute
                ,ServiceReflector.SMMessageBodyMemberAttributeFullName
                , ServiceReflector.SMMessageContractAttributeFullName
                ,ServiceReflector.SMMessageHeaderAttributeFullName
            };
            foreach (Attribute attr in memberInfo.GetCustomAttributes())
            {
                if (eligibleMessageList.Contains(attr.GetType().FullName))
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
                    || (String.Compare(attr.GetType().FullName, ServiceReflector.SMMessageHeaderAttributeFullName, true) == 0)
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
                    || (String.Compare(attr.GetType().FullName, ServiceReflector.SMMessagePropertyAttributeFullName, true) == 0))
                {
                    return true;
                }
            }
            return false;
        }



    }
}
