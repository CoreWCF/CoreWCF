// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CoreWCF
{
    internal static class DataContractHelpers
    {
        private static readonly Type s_typeOfIQueryable = typeof(IQueryable);
        private static readonly Type s_typeOfIQueryableGeneric = typeof(IQueryable<>);
        private static readonly Type s_typeOfIEnumerable = typeof(IEnumerable);
        private static readonly Type s_typeOfIEnumerableGeneric = typeof(IEnumerable<>);

        internal static Type GetSubstituteDataContractType(Type type, out bool isQueryable)
        {
            if (type == s_typeOfIQueryable)
            {
                isQueryable = true;
                return s_typeOfIEnumerable;
            }

            if (type.GetTypeInfo().IsGenericType &&
                type.GetGenericTypeDefinition() == s_typeOfIQueryableGeneric)
            {
                isQueryable = true;
                return s_typeOfIEnumerableGeneric.MakeGenericType(type.GetGenericArguments());
            }

            isQueryable = false;
            return type;
        }
    }
}
