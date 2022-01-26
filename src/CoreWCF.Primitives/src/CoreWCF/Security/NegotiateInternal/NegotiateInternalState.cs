// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NegotiateInternalState : INegotiateInternalState
    {
        // https://www.gnu.org/software/gss/reference/gss.pdf
        private const uint GSS_S_NO_CRED = 7 << 16;

        private static readonly ConstructorInfo s_constructor;
        private static readonly MethodInfo s_getOutgoingBlob;
        private static readonly MethodInfo s_isCompleted;
        private static readonly MethodInfo s_isValidContext;

        private static readonly MethodInfo s_protocol;
        private static readonly MethodInfo s_getIdentity;
        private static readonly MethodInfo s_closeContext;
        //private static readonly MethodInfo _getContext;
        private static readonly MethodInfo s_encrypt;

        private static readonly FieldInfo s_statusCode;
        private static readonly FieldInfo s_statusException;
        private static readonly FieldInfo s_gssMinorStatus;
        private static readonly Type s_gssExceptionType;

        private readonly object _instance;

        static NegotiateInternalState()
        {
            Assembly secAssembly = typeof(AuthenticationException).Assembly;
            Type ntAuthType = secAssembly.GetType("System.Net.NTAuthentication", throwOnError: true);
            s_constructor = ntAuthType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
            s_getOutgoingBlob = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("GetOutgoingBlob") && info.GetParameters().Count() == 3).Single();
            s_isCompleted = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_IsCompleted")).Single();
            s_protocol = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_ProtocolName")).Single();
            s_closeContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("CloseContext")).Single();

            //Added these 2 new call
            //   _getContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
            //       info.Name.Equals("GetContext")).Single();
            s_encrypt = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("Encrypt")).Single();
            s_isValidContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
            info.Name.Equals("get_IsValidContext")).Single();
            //end 

            //TODO this fails in framework
            Type securityStatusType = secAssembly.GetType("System.Net.SecurityStatusPal", throwOnError: true);
            // securityStatusType.get
            s_statusCode = securityStatusType.GetField("ErrorCode");
            s_statusException = securityStatusType.GetField("Exception");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Type interopType = secAssembly.GetType("Interop", throwOnError: true);
                Type netNativeType = interopType.GetNestedType("NetSecurityNative", BindingFlags.NonPublic | BindingFlags.Static);
                s_gssExceptionType = netNativeType.GetNestedType("GssApiException", BindingFlags.NonPublic);
                s_gssMinorStatus = s_gssExceptionType.GetField("_minorStatus", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Type negoStreamPalType = secAssembly.GetType("System.Net.Security.NegotiateStreamPal", throwOnError: true);
            s_getIdentity = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("GetIdentity")).Single();
            GetException = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("CreateExceptionFromError")).Single();
        }

        public NegotiateInternalState()
        {

            ICredentials credential = CredentialCache.DefaultCredentials;
            _instance = s_constructor.Invoke(new object[] { true, "Negotiate", credential, null, 0, null });
        }

        // Copied rather than reflected to remove the IsCompleted -> CloseContext check.
        // The client doesn't need the context once auth is complete, but the server does.
        // I'm not sure why it auto-closes for the client given that the client closes it just a few lines later.
        // https://github.com/dotnet/corefx/blob/a3ab91e10045bb298f48c1d1f9bd5b0782a8ac46/src/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/AuthenticationHelper.NtAuth.cs#L134
        public string GetOutgoingBlob(string incomingBlob, out BlobErrorType status, out Exception error)
        {
            byte[] decodedIncomingBlob = null;
            if (incomingBlob != null && incomingBlob.Length > 0)
            {
                decodedIncomingBlob = Convert.FromBase64String(incomingBlob);
            }

            byte[] decodedOutgoingBlob = GetOutgoingBlob(decodedIncomingBlob, out status, out error);

            string outgoingBlob = null;
            if (decodedOutgoingBlob != null && decodedOutgoingBlob.Length > 0)
            {
                outgoingBlob = Convert.ToBase64String(decodedOutgoingBlob);
            }

            return outgoingBlob;
        }

        public byte[] GetOutgoingBlob(byte[] incomingBlob, out BlobErrorType status, out Exception error)
        {
            try
            {
                // byte[] GetOutgoingBlob(byte[] incomingBlob, bool throwOnError, out SecurityStatusPal statusCode)
                object[] parameters = new object[] { incomingBlob, false, null };
                byte[] blob = (byte[])s_getOutgoingBlob.Invoke(_instance, parameters);

                object securityStatus = parameters[2];
                // TODO: Update after corefx changes
                error = (Exception)(s_statusException.GetValue(securityStatus)
                    ?? GetException.Invoke(null, new[] { securityStatus }));
                var errorCode = (NegotiateInternalSecurityStatusErrorCode)s_statusCode.GetValue(securityStatus);

                // TODO: Remove after corefx changes
                // The linux implementation always uses InternalError;
                if (errorCode == NegotiateInternalSecurityStatusErrorCode.InternalError
                    && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && s_gssExceptionType.IsInstanceOfType(error))
                {
                    uint majorStatus = (uint)error.HResult;
                    uint minorStatus = (uint)s_gssMinorStatus.GetValue(error);

                    // Remap specific errors
                    if (majorStatus == GSS_S_NO_CRED && minorStatus == 0)
                    {
                        errorCode = NegotiateInternalSecurityStatusErrorCode.UnknownCredentials;
                    }

                    error = new Exception($"An authentication exception occurred (0x{majorStatus:X}/0x{minorStatus:X}).", error);
                }

                if (errorCode == NegotiateInternalSecurityStatusErrorCode.OK
                    || errorCode == NegotiateInternalSecurityStatusErrorCode.ContinueNeeded
                    || errorCode == NegotiateInternalSecurityStatusErrorCode.CompleteNeeded)
                {
                    status = BlobErrorType.None;
                }
                else if (IsCredentialError(errorCode))
                {
                    status = BlobErrorType.CredentialError;
                }
                else if (IsClientError(errorCode))
                {
                    status = BlobErrorType.ClientError;
                }
                else
                {
                    status = BlobErrorType.Other;
                }

                return blob;
            }
            catch (TargetInvocationException tex)
            {
                // Unwrap
                ExceptionDispatchInfo.Capture(tex.InnerException).Throw();
                throw;
            }
        }

        public bool IsCompleted => (bool)s_isCompleted.Invoke(_instance, Array.Empty<object>());

        public string Protocol => (string)s_protocol.Invoke(_instance, Array.Empty<object>());

        public bool IsValidContext => (bool)s_isValidContext.Invoke(_instance, Array.Empty<object>());

        public static MethodInfo GetException { get; private set; }

        public IIdentity GetIdentity() => (IIdentity)s_getIdentity.Invoke(obj: null, parameters: new object[] { _instance });

        public byte[] Encrypt(byte[] input)
        {

            /*
             *     internal int Encrypt(
      byte[] buffer,
      int offset,
      int count,
      ref byte[] output,
      uint sequenceNumber)
             */
            if (input == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(input));
            }

            byte[] _writeBuffer = new byte[4];
            object[] parameters = new object[] { input, 0, input.Length, _writeBuffer, 0U };
            int totalBytes = (int)s_encrypt.Invoke(_instance, parameters);
            byte[] result = new byte[totalBytes - 4];
            Buffer.BlockCopy((byte[])parameters[3], 4, result, 0, result.Length);
            return result;
        }

        public void Dispose() => s_closeContext.Invoke(_instance, Array.Empty<object>());

        private bool IsCredentialError(NegotiateInternalSecurityStatusErrorCode error) => error == NegotiateInternalSecurityStatusErrorCode.LogonDenied ||
                error == NegotiateInternalSecurityStatusErrorCode.UnknownCredentials ||
                error == NegotiateInternalSecurityStatusErrorCode.NoImpersonation ||
                error == NegotiateInternalSecurityStatusErrorCode.NoAuthenticatingAuthority ||
                error == NegotiateInternalSecurityStatusErrorCode.UntrustedRoot ||
                error == NegotiateInternalSecurityStatusErrorCode.CertExpired ||
                error == NegotiateInternalSecurityStatusErrorCode.SmartcardLogonRequired ||
                error == NegotiateInternalSecurityStatusErrorCode.BadBinding;

        private bool IsClientError(NegotiateInternalSecurityStatusErrorCode error) => error == NegotiateInternalSecurityStatusErrorCode.InvalidToken ||
                error == NegotiateInternalSecurityStatusErrorCode.CannotPack ||
                error == NegotiateInternalSecurityStatusErrorCode.QopNotSupported ||
                error == NegotiateInternalSecurityStatusErrorCode.NoCredentials ||
                error == NegotiateInternalSecurityStatusErrorCode.MessageAltered ||
                error == NegotiateInternalSecurityStatusErrorCode.OutOfSequence ||
                error == NegotiateInternalSecurityStatusErrorCode.IncompleteMessage ||
                error == NegotiateInternalSecurityStatusErrorCode.IncompleteCredentials ||
                error == NegotiateInternalSecurityStatusErrorCode.WrongPrincipal ||
                error == NegotiateInternalSecurityStatusErrorCode.TimeSkew ||
                error == NegotiateInternalSecurityStatusErrorCode.IllegalMessage ||
                error == NegotiateInternalSecurityStatusErrorCode.CertUnknown ||
                error == NegotiateInternalSecurityStatusErrorCode.AlgorithmMismatch ||
                error == NegotiateInternalSecurityStatusErrorCode.SecurityQosFailed ||
                error == NegotiateInternalSecurityStatusErrorCode.UnsupportedPreauth;
    }
}
