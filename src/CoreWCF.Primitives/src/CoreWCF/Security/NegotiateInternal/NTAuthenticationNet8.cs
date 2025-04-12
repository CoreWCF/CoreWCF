// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationNet8 : INTAuthenticationFacade
    {
        private static readonly object[] serverOptions;
        private static readonly Type negotiateAuthenticationType;
        private static readonly Type negotiateAuthenticationStatusCodeType;
        private static readonly MethodInfo getOutgoingBlob;

        // value should match the Windows sspicli NTE_FAIL value
        // defined in winerror.h
        private const int NTE_FAIL = unchecked((int)0x80090020);

        private static readonly Delegate getOutgoingBlobInvoker;

        static NTAuthenticationNet8()
        {
            var securityAssembly = typeof(System.Net.Security.NegotiateStream).Assembly;
            var serverOptionsType = securityAssembly.GetType("System.Net.Security.NegotiateAuthenticationServerOptions", true);

            serverOptions = new object[1] { Activator.CreateInstance(serverOptionsType) };

            negotiateAuthenticationType = securityAssembly.GetType("System.Net.Security.NegotiateAuthentication", true);

            negotiateAuthenticationStatusCodeType = securityAssembly.GetType("System.Net.Security.NegotiateAuthenticationStatusCode", true);

            getOutgoingBlob = negotiateAuthenticationType.GetMethods().Single(m =>
                "GetOutgoingBlob".Equals(m.Name, StringComparison.Ordinal) &&
                typeof(byte[]).Equals(m.ReturnType));

            getOutgoingBlobInvoker = new LambdaExpressionBuilder(
                negotiateAuthenticationType,
                getOutgoingBlob).Compile();
        }

        private static IDisposable NewNegotiateAuthentication()
        {
            return (IDisposable)Activator.CreateInstance(negotiateAuthenticationType, serverOptions);
        }

        private readonly IDisposable _negotiateAuthentication;

        public NTAuthenticationNet8()
        {
            _negotiateAuthentication = NewNegotiateAuthentication();
        }

        // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.isauthenticated?view=net-8.0
        public bool IsCompleted => ((dynamic)_negotiateAuthentication).IsAuthenticated;

        // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.package?view=net-8.0
        public string Protocol => ((dynamic)_negotiateAuthentication).Package;

        public bool IsValidContext { get; private set; } = false;

        public byte[] Encrypt(byte[] input)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.wrap?view=net-8.0
            // System.Net.Security.NegotiateAuthenticationStatusCode Wrap(ReadOnlySpan<byte> input, System.Buffers.IBufferWriter<byte> outputWriter, bool requestEncryption, out bool isEncrypted);
            var writer = new ArrayBufferWriter<byte>();
            var statusCode = (int)((dynamic)_negotiateAuthentication).Wrap(input, writer, false, out bool isEncrypted);
            var output = writer.WrittenMemory.ToArray();

            return output;
        }

        public IIdentity GetIdentity()
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.remoteidentity?view=net-8.0
            return ((dynamic)_negotiateAuthentication).RemoteIdentity;
        }

        public byte[] GetOutgoingBlob(byte[] incomingBlob, out NegotiateInternalSecurityStatusPal status) 
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.getoutgoingblob?view=net-8.0#system-net-security-negotiateauthentication-getoutgoingblob(system-readonlyspan((system-byte))-system-net-security-negotiateauthenticationstatuscode@)
            // byte[]? GetOutgoingBlob(ReadOnlySpan<byte> incomingBlob, out System.Net.Security.NegotiateAuthenticationStatusCode statusCode);
            object statusCode = Activator.CreateInstance(negotiateAuthenticationStatusCodeType);
            var result = (byte[]) getOutgoingBlobInvoker.DynamicInvoke(
                _negotiateAuthentication,
                incomingBlob,
                statusCode);

            var internalStatusCode = ToErrorCode((int)statusCode);

            IsValidContext = internalStatusCode is NegotiateInternalSecurityStatusErrorCode.OK or NegotiateInternalSecurityStatusErrorCode.ContinueNeeded or NegotiateInternalSecurityStatusErrorCode.CompleteNeeded;

            Exception error = null;

            if (!IsValidContext)
            {
                error = new Win32Exception(NTE_FAIL, statusCode.ToString());
            }

            status = new NegotiateInternalSecurityStatusPal(internalStatusCode, error);

            return result;
        }

        private static bool IsNegotiateAuthenticationStatusCodeSuccessful(int statusCode)
        {
            // Completed || ContinueNeeded
            return statusCode == 0 || statusCode == 1;
        }

        public void Dispose()
        {
            _negotiateAuthentication.Dispose();
        }

        public static NegotiateInternalSecurityStatusErrorCode ToErrorCode(int inputValue)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthenticationstatuscode?view=net-8.0
            return inputValue switch
            {
                // Completed - Operation completed successfully
                0 => NegotiateInternalSecurityStatusErrorCode.OK,
                // ContinueNeeded
                1 => NegotiateInternalSecurityStatusErrorCode.ContinueNeeded,
                // GenericFailure
                2 => NegotiateInternalSecurityStatusErrorCode.InternalError,
                // BadBinding
                3 => NegotiateInternalSecurityStatusErrorCode.BadBinding,
                // Unsupported
                4 => NegotiateInternalSecurityStatusErrorCode.Unsupported,
                // MessageAltered
                5 => NegotiateInternalSecurityStatusErrorCode.MessageAltered,
                // ContextExpired
                6 => NegotiateInternalSecurityStatusErrorCode.ContextExpired,
                // CredentialsExpired (Closest match)
                7 => NegotiateInternalSecurityStatusErrorCode.UnknownCredentials,
                // InvalidCredentials (Closest match)
                8 => NegotiateInternalSecurityStatusErrorCode.UnknownCredentials,
                // InvalidToken
                9 => NegotiateInternalSecurityStatusErrorCode.InvalidToken,
                // UnknownCredentials
                10 => NegotiateInternalSecurityStatusErrorCode.UnknownCredentials,
                // QopNotSupported
                11 => NegotiateInternalSecurityStatusErrorCode.QopNotSupported,
                // OutOfSequence
                12 => NegotiateInternalSecurityStatusErrorCode.OutOfSequence,
                // SecurityQosFailed
                13 => NegotiateInternalSecurityStatusErrorCode.SecurityQosFailed,
                // TargetUnknown
                14 => NegotiateInternalSecurityStatusErrorCode.TargetUnknown,
                // ImpersonationValidationFailed (Closest match)
                15 => NegotiateInternalSecurityStatusErrorCode.NoImpersonation,
                _ => NegotiateInternalSecurityStatusErrorCode.NotSet,
            };
        }
    }
}
