// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationNet8 : INTAuthenticationFacade
    {
        // value should match the Windows sspicli NTE_FAIL value
        // defined in winerror.h
        private const int NTE_FAIL = unchecked((int)0x80090020);

        private static readonly Type s_negotiateAuthenticationType;
        private static readonly Type s_negotiateAuthenticationStatusCodeType;
        private static readonly MethodInfo s_getOutgoingBlob;
        private static readonly Delegate s_getOutgoingBlobInvoker;
        private static readonly Type s_serverOptionsType;

        static NTAuthenticationNet8()
        {
            var securityAssembly = typeof(System.Net.Security.NegotiateStream).Assembly;
            s_serverOptionsType = securityAssembly.GetType("System.Net.Security.NegotiateAuthenticationServerOptions", true);

            s_negotiateAuthenticationType = securityAssembly.GetType("System.Net.Security.NegotiateAuthentication", true);

            s_negotiateAuthenticationStatusCodeType = securityAssembly.GetType("System.Net.Security.NegotiateAuthenticationStatusCode", true);

            s_getOutgoingBlob = s_negotiateAuthenticationType.GetMethods().Single(m =>
                "GetOutgoingBlob".Equals(m.Name, StringComparison.Ordinal) &&
                typeof(byte[]).Equals(m.ReturnType));

            s_getOutgoingBlobInvoker = LambdaExpressionBuilder.BuildFor(
                s_negotiateAuthenticationType,
                s_getOutgoingBlob).Compile();
        }


        private static IDisposable CreateNegotiateAuthentication(object serverOptions)
        {
            object[] parameters = new object[] { serverOptions };
            return (IDisposable)Activator.CreateInstance(s_negotiateAuthenticationType, parameters);
        }

        private IDisposable _negotiateAuthentication;
        private ChannelBinding _channelBinding;
        private ExtendedProtectionPolicy _protectionPolicy;

        public NTAuthenticationNet8()
        {
            //_negotiateAuthentication = NewNegotiateAuthentication();
        }

        private object CreateNegotiateAuthenticationServerOptions()
        {
            dynamic serverOptions = Activator.CreateInstance(s_serverOptionsType);
            if (_channelBinding != null) serverOptions.ChannelBinding = _channelBinding;
            if (_protectionPolicy != null) serverOptions.ExtendedProtectionPolicy = _protectionPolicy;

            return serverOptions;
        }

        private dynamic NegotiateAuthentication => _negotiateAuthentication ??= CreateNegotiateAuthentication(CreateNegotiateAuthenticationServerOptions());

        public void SetChannelBinding(ChannelBinding channelBinding)
        {
            if (channelBinding == null) return;
            if (_negotiateAuthentication != null) throw new InvalidOperationException("Channel binding must be set before any authentication operations are performed.");
            if (_channelBinding != null && channelBinding != null && _channelBinding != channelBinding) throw new InvalidOperationException("Channel binding can only be set once.");

            _channelBinding = channelBinding;
        }

        public void SetExtendedProtectionPolicy(ExtendedProtectionPolicy protectionPolicy)
        {
            if (protectionPolicy == null) return;
            if (_negotiateAuthentication != null) throw new InvalidOperationException("Extended Protection Policy must be set before any authentication operations are performed.");
            if (_protectionPolicy != null && protectionPolicy != null && _protectionPolicy != protectionPolicy) throw new InvalidOperationException("Extended Protection Policy can only be set once.");

            _protectionPolicy = protectionPolicy;
        }

        // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.isauthenticated?view=net-8.0
        public bool IsCompleted => ((dynamic)NegotiateAuthentication).IsAuthenticated;

        // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.package?view=net-8.0
        public string Protocol => ((dynamic)NegotiateAuthentication).Package;

        public bool IsValidContext { get; private set; } = false;

        public byte[] Encrypt(byte[] input)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.wrap?view=net-8.0
            // System.Net.Security.NegotiateAuthenticationStatusCode Wrap(ReadOnlySpan<byte> input, System.Buffers.IBufferWriter<byte> outputWriter, bool requestEncryption, out bool isEncrypted);
            // Create the memory stream with an initial capacity twice the size of the input, as encryption may increase the size of the data.
            // This is a heuristic and will be more than enough for typical cases, but it allows us to avoid resizing the buffer in most cases.
            // If we're wrong (e.g. the encryption overhead is larger than the input size), MemoryStream will grow if needed.
            var memoryStream = new MemoryStream(input.Length * 2);
            PipeWriter pipeWriter = PipeWriter.Create(memoryStream);
            var statusCode = (int)(((dynamic)_negotiateAuthentication).Wrap(input, (IBufferWriter<byte>)pipeWriter, false, out bool isEncrypted));
            // Safe to call GetAwaiter().GetResult() as PipeWriter is on top of a MemoryStream which does all writes synchronously.
            pipeWriter.FlushAsync().GetAwaiter().GetResult();
            var output = memoryStream.ToArray();
            return output;
        }

        public IIdentity GetIdentity()
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.remoteidentity?view=net-8.0
            return ((dynamic)NegotiateAuthentication).RemoteIdentity;
        }

        public byte[] GetOutgoingBlob(byte[] incomingBlob, out NegotiateInternalSecurityStatusPal status)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication.getoutgoingblob?view=net-8.0#system-net-security-negotiateauthentication-getoutgoingblob(system-readonlyspan((system-byte))-system-net-security-negotiateauthenticationstatuscode@)
            // byte[]? GetOutgoingBlob(ReadOnlySpan<byte> incomingBlob, out System.Net.Security.NegotiateAuthenticationStatusCode statusCode);
            object statusCode = Activator.CreateInstance(s_negotiateAuthenticationStatusCodeType);

            object[] parameters = new object[] { NegotiateAuthentication, incomingBlob, statusCode };
            var result = (byte[]) s_getOutgoingBlobInvoker.DynamicInvoke(parameters);
            statusCode = parameters[2];

            var internalStatusCode = ToErrorCode((int)statusCode);

            IsValidContext = internalStatusCode is NegotiateInternalSecurityStatusErrorCode.OK
                or NegotiateInternalSecurityStatusErrorCode.ContinueNeeded
                or NegotiateInternalSecurityStatusErrorCode.CompleteNeeded;

            Exception error = null;

            if (!IsValidContext)
            {
                error = new Win32Exception(NTE_FAIL, statusCode.ToString());
            }

            status = new NegotiateInternalSecurityStatusPal(internalStatusCode, error);

            return result;
        }

        public void Dispose()
        {
            _negotiateAuthentication?.Dispose();
        }

        /// <summary>
        /// Convert the NegotiateAuthenticationStatusCode int value into the (likely corresponding)
        /// NegotiateInternalSecurityStatusErrorCode value
        /// </summary>
        private static NegotiateInternalSecurityStatusErrorCode ToErrorCode(int inputValue)
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
