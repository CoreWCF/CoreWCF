// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Generates Reflection.Emit-backed derived types of <see cref="InterceptingDuplexSessionChannel"/>
    /// and <see cref="InterceptingDuplexSession"/> that additionally implement the
    /// <c>System.ServiceModel.Channels.IAsyncDuplexSession</c> and
    /// <c>System.ServiceModel.Channels.ISessionChannel&lt;IAsyncDuplexSession&gt;</c> interfaces.
    ///
    /// These two interfaces are <b>internal</b> to System.ServiceModel.Primitives and therefore
    /// cannot be referenced from C# source. The System.ServiceModel client reliable session
    /// binder hard-casts the underlying duplex channel to <c>ISessionChannel&lt;IAsyncDuplexSession&gt;</c>,
    /// so any test wrapper inserted between the reliable session and the transport must
    /// provide that interface or the cast throws InvalidCastException.
    ///
    /// The emitted proxy classes are empty bodies that simply add the interface declarations:
    /// the inherited public members from the C# base classes satisfy the interface contracts
    /// by name and signature (implicit interface implementation rules).
    /// </summary>
    internal static class InterceptorRuntimeProxies
    {
        private static readonly Type s_iAsyncDuplexSession;
        private static readonly Type s_iSessionChannelOfAsync;
        private static readonly Type s_sessionProxyType;
        private static readonly Type s_channelProxyType;
        private static readonly MethodInfo s_innerCloseOutputSessionAsync;
        private static readonly MethodInfo s_innerCloseOutputSessionAsyncTimeout;

        static InterceptorRuntimeProxies()
        {
            Assembly primitives = typeof(IDuplexSessionChannel).Assembly;
            s_iAsyncDuplexSession = primitives.GetType("System.ServiceModel.Channels.IAsyncDuplexSession", throwOnError: false);
            if (s_iAsyncDuplexSession == null)
            {
                // Older System.ServiceModel.Primitives without IAsyncDuplexSession (e.g. .NET Framework
                // pre-async builds). Nothing to emit; the binder won't perform the cast either.
                return;
            }

            s_iSessionChannelOfAsync = typeof(ISessionChannel<>).MakeGenericType(s_iAsyncDuplexSession);

            s_innerCloseOutputSessionAsync = s_iAsyncDuplexSession.GetMethod(
                "CloseOutputSessionAsync", Type.EmptyTypes);
            s_innerCloseOutputSessionAsyncTimeout = s_iAsyncDuplexSession.GetMethod(
                "CloseOutputSessionAsync", new[] { typeof(TimeSpan) });

            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("CoreWCF.Tests.Interceptor.RuntimeProxies"),
                AssemblyBuilderAccess.Run);

            // Grant the dynamic assembly access to the internal types of System.ServiceModel.Primitives
            // (specifically IAsyncDuplexSession). This is honored by the .NET Core / .NET 5+ runtime;
            // .NET Framework ignores it (tests that need this must be NetCoreOnly).
            ConstructorInfo ignoreCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute)
                .GetConstructor(new[] { typeof(string) });
            asm.SetCustomAttribute(new CustomAttributeBuilder(ignoreCtor, new object[] { primitives.GetName().Name }));

            ModuleBuilder mod = asm.DefineDynamicModule("Main");

            s_sessionProxyType = BuildSessionProxy(mod);
            s_channelProxyType = BuildChannelProxy(mod);
        }

        public static InterceptingDuplexSession CreateSession(IDuplexSessionChannel innerChannel, InterceptingDuplexSessionChannel owner)
        {
            if (s_sessionProxyType == null)
            {
                return new InterceptingDuplexSession(innerChannel, owner);
            }
            return (InterceptingDuplexSession)Activator.CreateInstance(s_sessionProxyType, innerChannel, owner);
        }

        public static InterceptingDuplexSessionChannel CreateChannel(IDuplexSessionChannel inner, IMessageInterceptor interceptor)
        {
            if (s_channelProxyType == null)
            {
                return new InterceptingDuplexSessionChannel(inner, interceptor);
            }
            return (InterceptingDuplexSessionChannel)Activator.CreateInstance(s_channelProxyType, inner, interceptor);
        }

        public static object TryGetAsyncSession(IDuplexSessionChannel innerChannel)
        {
            if (s_iSessionChannelOfAsync == null)
            {
                return null;
            }
            if (!s_iSessionChannelOfAsync.IsAssignableFrom(innerChannel.GetType()))
            {
                return null;
            }
            PropertyInfo sessionProp = s_iSessionChannelOfAsync.GetProperty("Session");
            return sessionProp.GetValue(innerChannel);
        }

        public static Task InvokeCloseOutputSessionAsync(object asyncSession)
        {
            return (Task)s_innerCloseOutputSessionAsync.Invoke(asyncSession, null);
        }

        public static Task InvokeCloseOutputSessionAsync(object asyncSession, TimeSpan timeout)
        {
            return (Task)s_innerCloseOutputSessionAsyncTimeout.Invoke(asyncSession, new object[] { timeout });
        }

        private static Type BuildSessionProxy(ModuleBuilder mod)
        {
            // class InterceptingDuplexSession_AsyncProxy : InterceptingDuplexSession, IAsyncDuplexSession
            // The CLR does NOT automatically bind inherited public methods to a freshly added
            // interface (especially when the base class lives in another assembly), so we have
            // to emit explicit override stubs that delegate to the inherited methods.
            TypeBuilder tb = mod.DefineType(
                "InterceptingDuplexSession_AsyncProxy",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(InterceptingDuplexSession),
                new[] { s_iAsyncDuplexSession });

            ConstructorInfo baseCtor = typeof(InterceptingDuplexSession).GetConstructor(
                new[] { typeof(IDuplexSessionChannel), typeof(InterceptingDuplexSessionChannel) });

            ConstructorBuilder ctor = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(IDuplexSessionChannel), typeof(InterceptingDuplexSessionChannel) });
            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);

            // Emit forwards for every method of every interface in IAsyncDuplexSession's
            // interface tree (IAsyncDuplexSession, IDuplexSession, IInputSession, IOutputSession,
            // ISession). Each maps to a base class method by exact name + parameter signature.
            EmitInterfaceForwarders(tb, typeof(InterceptingDuplexSession), s_iAsyncDuplexSession);

            return tb.CreateTypeInfo().AsType();
        }

        private static void EmitInterfaceForwarders(TypeBuilder tb, Type baseClass, Type rootInterface)
        {
            var visited = new System.Collections.Generic.HashSet<Type>();
            EmitOne(tb, baseClass, rootInterface, visited);
        }

        private static void EmitOne(TypeBuilder tb, Type baseClass, Type iface, System.Collections.Generic.HashSet<Type> visited)
        {
            if (!visited.Add(iface))
            {
                return;
            }

            foreach (Type parent in iface.GetInterfaces())
            {
                EmitOne(tb, baseClass, parent, visited);
            }

            foreach (MethodInfo ifaceMethod in iface.GetMethods())
            {
                Type[] paramTypes = ParamTypes(ifaceMethod);
                MethodInfo baseMethod = baseClass.GetMethod(
                    ifaceMethod.Name,
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: paramTypes,
                    modifiers: null);
                if (baseMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Base class {baseClass.Name} has no public instance method {ifaceMethod.Name}({string.Join(",", System.Linq.Enumerable.Select(paramTypes, p => p.Name))}) to forward to for interface {iface.Name}.");
                }

                MethodBuilder mb = tb.DefineMethod(
                    iface.Name + "." + ifaceMethod.Name,
                    MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.HideBySig
                        | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                    ifaceMethod.ReturnType,
                    paramTypes);
                ILGenerator il = mb.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    il.Emit(OpCodes.Ldarg, i + 1);
                }
                il.Emit(OpCodes.Call, baseMethod);
                il.Emit(OpCodes.Ret);

                tb.DefineMethodOverride(mb, ifaceMethod);
            }
        }

        private static Type[] ParamTypes(MethodInfo m)
        {
            ParameterInfo[] ps = m.GetParameters();
            Type[] result = new Type[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                result[i] = ps[i].ParameterType;
            }
            return result;
        }

        private static Type BuildChannelProxy(ModuleBuilder mod)
        {
            // class InterceptingDuplexSessionChannel_AsyncProxy
            //     : InterceptingDuplexSessionChannel, ISessionChannel<IAsyncDuplexSession>
            // {
            //     ctor: forward
            //     IAsyncDuplexSession ISessionChannel<IAsyncDuplexSession>.get_Session()
            //         => (IAsyncDuplexSession)base.Session;
            // }
            TypeBuilder tb = mod.DefineType(
                "InterceptingDuplexSessionChannel_AsyncProxy",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(InterceptingDuplexSessionChannel),
                new[] { s_iSessionChannelOfAsync });

            ConstructorInfo baseCtor = typeof(InterceptingDuplexSessionChannel).GetConstructor(
                new[] { typeof(IDuplexSessionChannel), typeof(IMessageInterceptor) });

            ConstructorBuilder ctor = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(IDuplexSessionChannel), typeof(IMessageInterceptor) });
            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);

            // Explicit ISessionChannel<IAsyncDuplexSession>.Session getter.
            MethodInfo iSessionGetter = s_iSessionChannelOfAsync.GetProperty("Session").GetGetMethod();
            MethodBuilder getter = tb.DefineMethod(
                "ISessionChannel.IAsyncDuplexSession.get_Session",
                MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                s_iAsyncDuplexSession,
                Type.EmptyTypes);
            ILGenerator gil = getter.GetILGenerator();
            gil.Emit(OpCodes.Ldarg_0);
            gil.Emit(OpCodes.Call,
                typeof(InterceptingDuplexSessionChannel).GetProperty("Session").GetGetMethod());
            // base.Session returns IDuplexSession but the actual instance is the SessionProxy
            // emitted above which DOES implement IAsyncDuplexSession. Cast it.
            gil.Emit(OpCodes.Castclass, s_iAsyncDuplexSession);
            gil.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(getter, iSessionGetter);

            return tb.CreateTypeInfo().AsType();
        }
    }
}
