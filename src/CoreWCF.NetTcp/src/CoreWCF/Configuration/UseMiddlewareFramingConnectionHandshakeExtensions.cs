﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    /// <summary>
    /// Extension methods for adding typed middleware to a <see cref="IFramingConnectionHandshakeBuilder"/>.
    /// </summary>
    public static class UseMiddlewareFramingConnectionHandshakeExtensions
    {
        internal const string OnConnectedAsyncMethodName = "OnConnectedAsync";

        private static readonly MethodInfo s_getServiceInfo = typeof(UseMiddlewareFramingConnectionHandshakeExtensions).GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Adds a middleware type to the connection handshake pipeline.
        /// </summary>
        /// <typeparam name="TMiddleware">The middleware type.</typeparam>
        /// <param name="app">The <see cref="IFramingConnectionHandshakeBuilder"/> instance.</param>
        /// <param name="args">The arguments to pass to the middleware type instance's constructor.</param>
        /// <returns>The <see cref="IFramingConnectionHandshakeBuilder"/> instance.</returns>
        public static IFramingConnectionHandshakeBuilder UseMiddleware<TMiddleware>(this IFramingConnectionHandshakeBuilder app, params object[] args)
        {
            return app.UseMiddleware(typeof(TMiddleware), args);
        }

        /// <summary>
        /// Adds a middleware type to the connection handshake pipeline.
        /// </summary>
        /// <param name="app">The <see cref="IFramingConnectionHandshakeBuilder"/> instance.</param>
        /// <param name="middleware">The middleware type.</param>
        /// <param name="args">The arguments to pass to the middleware type instance's constructor.</param>
        /// <returns>The <see cref="IFramingConnectionHandshakeBuilder"/> instance.</returns>
        public static IFramingConnectionHandshakeBuilder UseMiddleware(this IFramingConnectionHandshakeBuilder app, Type middleware, params object[] args)
        {
            IServiceProvider handshakeServices = app.HandshakeServices;
            return app.Use(next =>
            {
                MethodInfo[] methods = middleware.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                MethodInfo[] invokeMethods = methods.Where(m =>
                    string.Equals(m.Name, OnConnectedAsyncMethodName, StringComparison.Ordinal)
                    ).ToArray();

                if (invokeMethods.Length > 1)
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddleMultipleInvokes({OnConnectedAsyncMethodName}");
                }

                if (invokeMethods.Length == 0)
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareNoInvokeMethod({OnConnectedAsyncMethodName}, {middleware}");
                }

                MethodInfo methodInfo = invokeMethods[0];
                if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareNonTaskReturnType({OnConnectedAsyncMethodName}, {nameof(Task)}");
                }

                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(FramingConnection))
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareNoParameters({OnConnectedAsyncMethodName}, {nameof(FramingConnection)}");
                }

                object[] ctorArgs = new object[args.Length + 1];
                ctorArgs[0] = next;
                Array.Copy(args, 0, ctorArgs, 1, args.Length);
                object instance = ActivatorUtilities.CreateInstance(app.HandshakeServices, middleware, ctorArgs);
                if (parameters.Length == 1)
                {
                    return (HandshakeDelegate)methodInfo.CreateDelegate(typeof(HandshakeDelegate), instance);
                }

                Func<object, FramingConnection, IServiceProvider, Task> factory = Compile<object>(methodInfo, parameters);

                return context =>
                {
                    IServiceProvider serviceProvider = handshakeServices;
                    if (serviceProvider == null)
                    {
                        // TODO: String resources
                        throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareIServiceProviderNotAvailable({nameof(IServiceProvider)}");
                    }

                    return factory(instance, context, serviceProvider);
                };
            });
        }

        private static Func<T, FramingConnection, IServiceProvider, Task> Compile<T>(MethodInfo methodInfo, ParameterInfo[] parameters)
        {
            // If we call something like
            //
            // public class Middleware
            // {
            //    public Task Invoke(ConnectionContext context, ILoggerFactory loggerFactory)
            //    {
            //
            //    }
            // }
            //

            // We'll end up with something like this:
            //   Generic version:
            //
            //   Task Invoke(Middleware instance, ConnectionContext httpContext, IServiceProvider provider)
            //   {
            //      return instance.Invoke(httpContext, (ILoggerFactory)UseMiddlewareConnectionHandshakeExtensions.GetService(provider, typeof(ILoggerFactory));
            //   }

            //   Non generic version:
            //
            //   Task Invoke(object instance, ConnectionContext httpContext, IServiceProvider provider)
            //   {
            //      return ((Middleware)instance).Invoke(httpContext, (ILoggerFactory)UseMiddlewareConnectionHandshakeExtensions.GetService(provider, typeof(ILoggerFactory));
            //   }

            Type middleware = typeof(T);

            ParameterExpression connectionContextArg = Expression.Parameter(typeof(FramingConnection), "connectionContext");
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
                    throw new NotSupportedException($"Resources.FormatException_InvokeDoesNotSupportRefOrOutParams({OnConnectedAsyncMethodName})");
                }

                var parameterTypeExpression = new Expression[]
                {
                    providerArg,
                    Expression.Constant(parameterType, typeof(Type)),
                    Expression.Constant(methodInfo.DeclaringType, typeof(Type))
                };

                MethodCallExpression getServiceCall = Expression.Call(s_getServiceInfo, parameterTypeExpression);
                methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
            }

            Expression middlewareInstanceArg = instanceArg;
            if (methodInfo.DeclaringType != typeof(T))
            {
                middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
            }

            MethodCallExpression body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);

            var lambda = Expression.Lambda<Func<T, FramingConnection, IServiceProvider, Task>>(body, instanceArg, connectionContextArg, providerArg);

            return lambda.Compile();
        }

        private static object GetService(IServiceProvider sp, Type type, Type middleware)
        {
            object service = sp.GetService(type);
            if (service == null)
            {
                // TODO: String resources
                throw new InvalidOperationException($"Resources.FormatException_InvokeMiddlewareNoService({type}, {middleware})");
            }

            return service;
        }
    }
}
