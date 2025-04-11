// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Reflection;
using System.Security.Authentication;
using System;
using System.Linq;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationLegacy : INTAuthenticationFacade
    {
        protected static readonly Type s_ntAuthenticationType = typeof(AuthenticationException).Assembly.GetType("System.Net.NTAuthentication", throwOnError: true);
        protected static readonly ConstructorInfo s_constructor;
        protected static readonly MethodInfo s_getOutgoingBlob;
        protected static readonly MethodInfo s_isCompleted;
        protected static readonly MethodInfo s_isValidContext;
        protected static readonly MethodInfo s_protocol;
        protected static readonly MethodInfo s_closeContext;
        protected static readonly MethodInfo s_encrypt;
        protected static readonly MethodInfo s_getIdentity;
        protected static readonly MethodInfo s_createExceptionFromError;

        protected object Instance { get; }

        public bool IsCompleted => (bool)s_isCompleted.Invoke(Instance, Array.Empty<object>());

        public string Protocol => (string)s_protocol.Invoke(Instance, Array.Empty<object>());

        public bool IsValidContext => (bool)s_isValidContext.Invoke(Instance, Array.Empty<object>());

        internal NTAuthenticationLegacy() => Instance = CreateInstance();

        static NTAuthenticationLegacy()
        {
            s_constructor = s_ntAuthenticationType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
            s_getOutgoingBlob = s_ntAuthenticationType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("GetOutgoingBlob") && info.ToString().Equals("Byte[] GetOutgoingBlob(Byte[], Boolean, System.Net.SecurityStatusPal ByRef)")).Single();
            s_isCompleted = s_ntAuthenticationType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_IsCompleted")).Single();
            s_protocol = s_ntAuthenticationType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_ProtocolName")).Single();
            s_closeContext = s_ntAuthenticationType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("CloseContext")).Single();
            s_encrypt = s_ntAuthenticationType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("Encrypt")).Single();
            s_isValidContext = s_ntAuthenticationType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
            info.Name.Equals("get_IsValidContext")).Single();

            Assembly secAssembly = typeof(AuthenticationException).Assembly;

            Type negoStreamPalType = secAssembly.GetType("System.Net.Security.NegotiateStreamPal", throwOnError: true);
            s_getIdentity = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("GetIdentity")).Single();
            s_createExceptionFromError = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("CreateExceptionFromError")).Single();
        }

        protected static object CreateInstance()
        {
            ICredentials credential = CredentialCache.DefaultCredentials;
            return s_constructor.Invoke(new object[] { true, "Negotiate", credential, null, 0, null });
        }

        public virtual byte[] GetOutgoingBlob(byte[] incomingBlob, bool throwOnError, out object statusCode)
        {
            // byte[] GetOutgoingBlob(byte[] incomingBlob, bool throwOnError, out SecurityStatusPal statusCode)
            object[] parameters = new object[] { incomingBlob, throwOnError, null };
            byte[] blob = (byte[])s_getOutgoingBlob.Invoke(Instance, parameters);
            statusCode = parameters[2];
            return blob;
        }

        public virtual void Dispose()
        {
            s_closeContext.Invoke(Instance, Array.Empty<object>());
        }

        public virtual int Encrypt(byte[] input, ref byte[] output)
        {
            /*
             * internal int Encrypt(
             *     byte[] buffer,
             *     int offset,
             *     int count,
             *     ref byte[] output,
             *     uint sequenceNumber)
             */
            object[] parameters = new object[] { input, 0, input.Length, output, 0U };
            int totalBytes = (int)s_encrypt.Invoke(Instance, parameters);
            output = (byte[])parameters[3];
            return totalBytes;
        }

        public IIdentity GetIdentity() => (IIdentity)s_getIdentity.Invoke(obj: null, parameters: new object[] { Instance });

        public Exception CreateExceptionFromError(object securityStatus) => (Exception) s_createExceptionFromError.Invoke(null, new[] { securityStatus });
    }
}
