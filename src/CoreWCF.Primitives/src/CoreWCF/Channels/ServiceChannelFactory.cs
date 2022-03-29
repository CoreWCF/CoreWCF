// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using CoreWCF.Description;

namespace CoreWCF.Channels
{
    public class ServiceChannelFactory
    {
        private delegate object CreateProxyDelegate(MessageDirection direction, ServiceChannel serviceChannel);
        private static readonly IDictionary<Type, CreateProxyDelegate> s_createProxyDelegateCache = new ConcurrentDictionary<Type, CreateProxyDelegate>();

        internal static object CreateProxy(Type interfaceType, Type proxiedType, MessageDirection direction, ServiceChannel serviceChannel)
        {
            if (!proxiedType.GetTypeInfo().IsInterface)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxChannelFactoryTypeMustBeInterface));
            }

            if (!s_createProxyDelegateCache.TryGetValue(proxiedType, out CreateProxyDelegate createProxyDelegate))
            {
                MethodInfo method = typeof(ServiceChannelFactory).GetMethod(nameof(CreateProxyWithType),
                    BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo generic = method.MakeGenericMethod(proxiedType);
                createProxyDelegate = (CreateProxyDelegate)generic.CreateDelegate(typeof(CreateProxyDelegate));
                s_createProxyDelegateCache[proxiedType] = createProxyDelegate;
            }

            return createProxyDelegate(direction, serviceChannel);
        }

        internal static object CreateProxyWithType<TChannel>(MessageDirection direction, ServiceChannel serviceChannel)
        {
            if (!typeof(TChannel).GetTypeInfo().IsInterface)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxChannelFactoryTypeMustBeInterface));
            }

#pragma warning disable CS0618 // Type or member is obsolete
            return ServiceChannelProxy.CreateProxy<TChannel>(direction, serviceChannel);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        internal static ServiceChannel GetServiceChannel(object transparentProxy)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (transparentProxy is ServiceChannelProxy proxy)
            {
                return proxy.GetServiceChannel();
            }
            else
            {
                return null;
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
