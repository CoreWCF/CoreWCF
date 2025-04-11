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
    /// Helper class to build a lambda expression to invoke a method that has a
    /// span input.
    /// Example of lambda: (object instance, byte[] input) => ((InternalType) instance).MyMethod((ReadOnlySpan&lt;byte&gt;input.AsSpan())
    /// </summary>
    internal class LambdaExpressionBuilder
    {
        private static readonly MethodInfo s_asSpan = typeof(MemoryExtensions)
                .GetMethods()
                .Single(m => m.Name == "AsSpan" && m.ToString() == "System.Span`1[T] AsSpan[T](T[])");

        private readonly IList<Expression> _callParameters = new List<Expression>();
        private readonly IList<ParameterExpression> _lambdaParameters = new List<ParameterExpression>();

        private readonly Type _targetType;
        private readonly MethodInfo _targetMethod;

        public LambdaExpressionBuilder(Type targetType, MethodInfo targetMethod)
        {
            _targetType = targetType;
            _targetMethod = targetMethod;


            _callParameters = new List<Expression>();
            _lambdaParameters = new List<ParameterExpression>
            {
                Expression.Parameter(typeof(object))
            };

            foreach (var methodParameter in targetMethod.GetParameters())
            {
                AddParameter(
                    methodParameter.ParameterType,
                    methodParameter.IsOut);
            }
        }

        public void AddParameter(Type parameterType, bool isOut)
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

        public void AddArrayParameter<TItem>(bool asSpan = false)
        {
            var parameter = Expression.Parameter(typeof(byte[]));
            var arrayType = typeof(TItem).MakeArrayType();
            MethodInfo asSpanMethod = s_asSpan.MakeGenericMethod(arrayType);
            UnaryExpression span = Expression.Convert(Expression.Call(asSpanMethod, parameter), typeof(ReadOnlySpan<TItem>));

            _callParameters.Add(span);
            _lambdaParameters.Add(parameter);
        }

        private bool IsSpan(Type type, out Type itemType)
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

        public Delegate Compile()
        {
            var instanceParameter = _lambdaParameters[0];
            var typedInstance = Expression.TypeAs(instanceParameter, _targetType);

            MethodCallExpression call = Expression.Call(
                typedInstance,
                _targetMethod,
                _callParameters);

            var expression = Expression.Lambda(call, _lambdaParameters.AsEnumerable());

            return expression.Compile();
        }
    }
}
