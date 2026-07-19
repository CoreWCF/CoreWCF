// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoreWCF.Security.NegotiateInternal
{
    /// <summary>
    /// Helper class to compile a lambda expression to invoke a method that has a
    /// span input with reflection.
    /// The span input are expected as array type.
    /// Example of generated lambda: (object instance, byte[] input) => ((TargetInternalType) instance).TargetMethod((ReadOnlySpan&lt;byte&gt;)input.AsSpan())
    /// </summary>
    internal static class LambdaExpressionBuilder
    {
        private static readonly MethodInfo s_asSpan = typeof(MemoryExtensions)
                .GetMethods()
                .Single(m => m.Name == "AsSpan" && m.ToString() == "System.Span`1[T] AsSpan[T](T[])");

        public static LambdaExpression BuildFor(Type targetType, MethodInfo targetMethod)
        {
            var _callParameters = new List<Expression>();
            var _lambdaParameters = new List<ParameterExpression>
            {
                Expression.Parameter(typeof(object))
            };

            void AddParameter(Type parameterType, bool isOut)
            {
                if (IsSpan(parameterType, out var itemType))
                {
                    var parameter = Expression.Parameter(itemType.MakeArrayType());
                    MethodInfo asSpanMethod = s_asSpan.MakeGenericMethod(itemType);
                    UnaryExpression span = Expression.Convert(
                        Expression.Call(asSpanMethod, parameter),
                        parameterType);

                    _callParameters.Add(span);
                    _lambdaParameters.Add(parameter);
                }
                else
                {
                    var parameter = Expression.Parameter(parameterType);
                    _callParameters.Add(parameter);
                    _lambdaParameters.Add(parameter);
                }
            }

            foreach (var methodParameter in targetMethod.GetParameters())
            {
                AddParameter(
                    methodParameter.ParameterType,
                    methodParameter.IsOut);
            }

            var instanceParameter = _lambdaParameters[0];
            var typedInstance = Expression.TypeAs(instanceParameter, targetType);

            MethodCallExpression call = Expression.Call(
                typedInstance,
                targetMethod,
                _callParameters);

            return Expression.Lambda(call, _lambdaParameters.AsEnumerable());
        }

        private static bool IsSpan(Type type, out Type itemType)
        {
            bool isSpan = (type.FullName.StartsWith("System.Span", StringComparison.Ordinal) || type.FullName.StartsWith("System.ReadOnlySpan", StringComparison.Ordinal))
                && type.GenericTypeArguments.Length == 1;

            if (isSpan)
            {
                itemType = type.GenericTypeArguments[0];
            }
            else
            {
                itemType = null;
            }

            return isSpan;
        }
    }
}
