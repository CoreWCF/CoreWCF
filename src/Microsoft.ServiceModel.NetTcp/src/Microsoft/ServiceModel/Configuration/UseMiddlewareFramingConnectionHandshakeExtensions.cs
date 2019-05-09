using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceModel.Channels.Framing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Configuration
{
    /// <summary>
    /// Extension methods for adding typed middleware to a <see cref="IFramingConnectionHandshakeBuilder"/>.
    /// </summary>
    public static class UseMiddlewareFramingConnectionHandshakeExtensions
    {
        internal const string OnConnectedAsyncMethodName = "OnConnectedAsync";

        private static readonly MethodInfo GetServiceInfo = typeof(UseMiddlewareFramingConnectionHandshakeExtensions).GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static);

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
            var handshakeServices = app.HandshakeServices;
            return app.Use(next =>
            {
                var methods = middleware.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                var invokeMethods = methods.Where(m =>
                    string.Equals(m.Name, OnConnectedAsyncMethodName, StringComparison.Ordinal)
                    ).ToArray();

                if (invokeMethods.Length > 1)
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddleMutlipleInvokes({OnConnectedAsyncMethodName}");
                }

                if (invokeMethods.Length == 0)
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareNoInvokeMethod({OnConnectedAsyncMethodName}, {middleware}");
                }

                var methodInfo = invokeMethods[0];
                if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareNonTaskReturnType({OnConnectedAsyncMethodName}, {nameof(Task)}");
                }

                var parameters = methodInfo.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(FramingConnection))
                {
                    // TODO: String resources
                    throw new InvalidOperationException($"Resources.FormatException_UseMiddlewareNoParameters({OnConnectedAsyncMethodName}, {nameof(FramingConnection)}");
                }

                var ctorArgs = new object[args.Length + 1];
                ctorArgs[0] = next;
                Array.Copy(args, 0, ctorArgs, 1, args.Length);
                var instance = ActivatorUtilities.CreateInstance(app.HandshakeServices, middleware, ctorArgs);
                if (parameters.Length == 1)
                {
                    return (HandshakeDelegate)methodInfo.CreateDelegate(typeof(HandshakeDelegate), instance);
                }

                var factory = Compile<object>(methodInfo, parameters);

                return context =>
                {
                    var serviceProvider = handshakeServices;
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

            var middleware = typeof(T);

            var connectionContextArg = Expression.Parameter(typeof(FramingConnection), "connectionContext");
            var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var instanceArg = Expression.Parameter(middleware, "middleware");

            var methodArguments = new Expression[parameters.Length];
            methodArguments[0] = connectionContextArg;
            for (int i = 1; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
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

                var getServiceCall = Expression.Call(GetServiceInfo, parameterTypeExpression);
                methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
            }

            Expression middlewareInstanceArg = instanceArg;
            if (methodInfo.DeclaringType != typeof(T))
            {
                middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
            }

            var body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);

            var lambda = Expression.Lambda<Func<T, FramingConnection, IServiceProvider, Task>>(body, instanceArg, connectionContextArg, providerArg);

            return lambda.Compile();
        }

        private static object GetService(IServiceProvider sp, Type type, Type middleware)
        {
            var service = sp.GetService(type);
            if (service == null)
            {
                // TODO: String resources
                throw new InvalidOperationException($"Resources.FormatException_InvokeMiddlewareNoService({type}, {middleware})");
            }

            return service;
        }
    }
}
