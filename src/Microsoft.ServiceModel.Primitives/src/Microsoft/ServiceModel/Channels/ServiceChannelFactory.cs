using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.ServiceModel.Description;

namespace Microsoft.ServiceModel.Channels
{
    public class ServiceChannelFactory
    {
        private delegate object CreateProxyDelegate(MessageDirection direction, ServiceChannel serviceChannel);
        private static IDictionary<Type, CreateProxyDelegate> s_createProxyDelegateCache = new ConcurrentDictionary<Type, CreateProxyDelegate>();

        internal static object CreateProxy(Type interfaceType, Type proxiedType, MessageDirection direction, ServiceChannel serviceChannel)
        {
            if (!proxiedType.GetTypeInfo().IsInterface)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxChannelFactoryTypeMustBeInterface));
            }

            CreateProxyDelegate createProxyDelegate;
            if (!s_createProxyDelegateCache.TryGetValue(proxiedType, out createProxyDelegate))
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

            return ServiceChannelProxy.CreateProxy<TChannel>(direction, serviceChannel);
        }

        internal static ServiceChannel GetServiceChannel(object transparentProxy)
        {
            ServiceChannelProxy proxy = transparentProxy as ServiceChannelProxy;

            if (proxy != null)
                return proxy.GetServiceChannel();
            else
                return null;
        }
    }
}