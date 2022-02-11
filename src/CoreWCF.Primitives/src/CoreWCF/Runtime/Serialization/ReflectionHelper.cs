// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq.Expressions;
using System.Reflection;

namespace CoreWCF.Runtime.Serialization
{
    internal static class ReflectionHelper
    {
        internal static Func<object, TProp> GetPropertyDelegate<TProp>(Type declaringType, string propertyName)
        {
            Fx.Assert(declaringType != null, "declaringType can't be null");
            var propertyInfo = declaringType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Fx.Assert(propertyInfo != null, $"Type {declaringType.Name} doesn't have a property called {propertyName}");
            var propertyGetter = propertyInfo.GetGetMethod(nonPublic: true);
            return CreateInstanceMethodCallLambda<TProp>(propertyGetter, declaringType);
        }

        internal static Func<TInstance, TProp> GetPropertyDelegate<TInstance, TProp>(string propertyName)
        {
            var propertyInfo = typeof(TInstance).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Fx.Assert(propertyInfo != null, $"Type {typeof(TInstance).Name} doesn't have a property called {propertyName}");
            var propertyGetter = propertyInfo.GetGetMethod(nonPublic: true);
            return CreateInstanceMethodCallLambda<TInstance, TProp>(propertyGetter);
        }

        internal static Action<object, TProp> SetPropertyDelegate<TProp>(Type declaringType, string propertyName)
        {
            Fx.Assert(declaringType != null, "declaringType can't be null");
            var propertyInfo = declaringType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Fx.Assert(propertyInfo != null, $"Type {declaringType.Name} doesn't have a property called {propertyName}");
            var propertySetter = propertyInfo.GetSetMethod(nonPublic: true);
            return CreateVoidInstanceMethodCallWithParamLambda<TProp>(propertySetter, declaringType);
        }

        internal static Func<TParam, TReturn> CreateStaticMethodCallLambda<TParam, TReturn>(MethodInfo methodInfo)
        {
            var paramExpression = Expression.Parameter(typeof(TParam), "param");
            // Passing null as instance expression as static method call
            Expression callExpr = Expression.Call(null, methodInfo, paramExpression);
            if (typeof(TReturn) != typeof(object))
            {
                callExpr = Expression.Convert(callExpr, typeof(TReturn));
            }

            var lambdaExpr = Expression.Lambda<Func<TParam, TReturn>>(callExpr, paramExpression);
            return lambdaExpr.Compile();
        }

        internal static Func<object, TReturn> CreateInstanceMethodCallLambda<TReturn>(MethodInfo methodInfo, Type instanceType)
        {
            var instanceObjParamExpr = Expression.Parameter(typeof(object), "instance");
            var typeInstanceExpr = Expression.Convert(instanceObjParamExpr, instanceType);
            Expression callExpr = Expression.Call(typeInstanceExpr, methodInfo); // No parameters for this method
            if (typeof(TReturn) != typeof(object))
            {
                callExpr = Expression.Convert(callExpr, typeof(TReturn));
            }

            var lambdaExpr = Expression.Lambda<Func<object, TReturn>>(callExpr, instanceObjParamExpr);
            return lambdaExpr.Compile();
        }

        internal static Func<TInstance, TReturn> CreateInstanceMethodCallLambda<TInstance, TReturn>(MethodInfo methodInfo)
        {
            var instanceParamExpr = Expression.Parameter(typeof(TInstance), "instance");
            Expression callExpr = Expression.Call(instanceParamExpr, methodInfo); // No parameters for this method
            if (typeof(TReturn) != typeof(object))
            {
                callExpr = Expression.Convert(callExpr, typeof(TReturn));
            }

            var lambdaExpr = Expression.Lambda<Func<TInstance, TReturn>>(callExpr, instanceParamExpr);
            return lambdaExpr.Compile();
        }

        internal static Action<object, TParam> CreateVoidInstanceMethodCallWithParamLambda<TParam>(MethodInfo methodInfo, Type instanceType)
        {
            var instanceObjParamExpr = Expression.Parameter(typeof(object), "instance");
            var typeInstanceExpr = Expression.Convert(instanceObjParamExpr, instanceType);
            var firstParamParamExpr = Expression.Parameter(typeof(TParam), "param");
            Expression callExpr = Expression.Call(typeInstanceExpr, methodInfo, firstParamParamExpr);
            var lambdaExpr = Expression.Lambda<Action<object, TParam>>(callExpr, instanceObjParamExpr, firstParamParamExpr);
            return lambdaExpr.Compile();
        }
    }
}
