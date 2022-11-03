﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common.Configuration
{
    public static class QueueMiddlewareBuilderExtension
    {
        private const string InvokeAsyncMethodName = "InvokeAsync";

        public static IQueueMiddlewareBuilder UseMiddleware<TMiddleware>(this IQueueMiddlewareBuilder app,
            params object[] args)
        {
            return app.UseMiddleware(typeof(TMiddleware), args);
        }

        public static IQueueMiddlewareBuilder UseMiddleware(this IQueueMiddlewareBuilder app, Type middleware,
            params object[] args)
        {
            IServiceProvider handshakeServices = app.Services;
            return app.Use(next =>
            {
                MethodInfo[] methods = middleware.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                MethodInfo[] invokeMethods = methods.Where(m =>
                    string.Equals(m.Name, InvokeAsyncMethodName, StringComparison.Ordinal)
                ).ToArray();

                if (invokeMethods.Length > 1)
                {
                    // TODO: String resources
                    throw new InvalidOperationException(
                        $"Resources.FormatException_UseMiddleMultipleInvokes({InvokeAsyncMethodName}");
                }

                if (invokeMethods.Length == 0)
                {
                    // TODO: String resources
                    throw new InvalidOperationException(
                        $"Resources.FormatException_UseMiddlewareNoInvokeMethod({InvokeAsyncMethodName}, {middleware}");
                }

                MethodInfo methodInfo = invokeMethods[0];
                if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                {
                    // TODO: String resources
                    throw new InvalidOperationException(
                        $"Resources.FormatException_UseMiddlewareNonTaskReturnType({InvokeAsyncMethodName}, {nameof(Task)}");
                }

                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(QueueMessageContext))
                {
                    // TODO: String resources
                    throw new InvalidOperationException(
                        $"Resources.FormatException_UseMiddlewareNoParameters({InvokeAsyncMethodName}, {nameof(QueueMessageContext)}");
                }

                object[] ctorArgs = new object[args.Length + 1];
                ctorArgs[0] = next;
                Array.Copy(args, 0, ctorArgs, 1, args.Length);
                object instance = ActivatorUtilities.CreateInstance(app.Services, middleware, ctorArgs);
                if (parameters.Length == 1)
                {
                    return (QueueMessageDispatcherDelegate)methodInfo.CreateDelegate(typeof(QueueMessageDispatcherDelegate), instance);
                }

                Func<object, QueueMessageContext, IServiceProvider, Task> factory =
                    Compile<object>(methodInfo, parameters);

                return context =>
                {
                    IServiceProvider serviceProvider = handshakeServices;
                    if (serviceProvider == null)
                    {
                        // TODO: String resources
                        throw new InvalidOperationException(
                            $"Resources.FormatException_UseMiddlewareIServiceProviderNotAvailable({nameof(IServiceProvider)}");
                    }

                    return factory(instance, context, serviceProvider);
                };
            });
        }


        private static Func<T, QueueMessageContext, IServiceProvider, Task> Compile<T>(MethodInfo methodInfo,
            ParameterInfo[] parameters)
        {
            Type middleware = typeof(T);
            ParameterExpression connectionContextArg =
                Expression.Parameter(typeof(QueueMessageContext), "connectionContext");
            ParameterExpression providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            ParameterExpression instanceArg = Expression.Parameter(middleware, "middleware");

            var methodArguments = new Expression[parameters.Length];
            methodArguments[0] = connectionContextArg;
            for (int i = 1; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType.IsByRef)
                {
                    // TODO: String resources
                    throw new NotSupportedException(
                        $"Resources.FormatException_InvokeDoesNotSupportRefOrOutParams({InvokeAsyncMethodName})");
                }

                var parameterTypeExpression = new Expression[]
                {
                    providerArg, Expression.Constant(parameterType, typeof(Type)),
                    Expression.Constant(methodInfo.DeclaringType, typeof(Type))
                };

                var getServiceInfo = typeof(QueueMiddlewareBuilderExtension).GetMethod(nameof(GetService),
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (getServiceInfo == null)
                {
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareGetServiceMethodNotFound({nameof(IServiceProvider)}");
                }

                MethodCallExpression getServiceCall = Expression.Call(getServiceInfo, parameterTypeExpression);
                methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
            }

            Expression middlewareInstanceArg = instanceArg;
            if (methodInfo.DeclaringType != typeof(T) && methodInfo.DeclaringType != null)
            {
                middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
            }

            MethodCallExpression body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);

            var lambda =
                Expression.Lambda<Func<T, QueueMessageContext, IServiceProvider, Task>>(body, instanceArg,
                    connectionContextArg, providerArg);

            return lambda.Compile();
        }

        private static object GetService(IServiceProvider sp, Type type, Type middleware)
        {
            object service = sp.GetService(type);
            if (service == null)
            {
                // TODO: String resources
                throw new InvalidOperationException(
                    $"Resources.FormatException_InvokeMiddlewareNoService({type}, {middleware})");
            }

            return service;
        }
    }
}
