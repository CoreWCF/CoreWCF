// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
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

        private static readonly MethodInfo s_getIdentity;

        private static readonly FieldInfo s_statusCode;
        private static readonly FieldInfo s_statusException;
        private static readonly FieldInfo s_gssMinorStatus;
        private static readonly Type s_gssExceptionType;

        private readonly INTAuthenticationFacade _ntAuthentication;

        static NegotiateInternalState()
        {
            Assembly secAssembly = typeof(AuthenticationException).Assembly;

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
            _ntAuthentication = NTAuthenticationFacade.Build();
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
                byte[] blob = _ntAuthentication.GetOutgoingBlob(incomingBlob, false, out object securityStatus);

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

        public bool IsCompleted => _ntAuthentication.IsCompleted;

        public string Protocol => _ntAuthentication.Protocol;

        public bool IsValidContext => _ntAuthentication.IsValidContext;

        public static MethodInfo GetException { get; private set; }

        public IIdentity GetIdentity() => (IIdentity)s_getIdentity.Invoke(obj: null, parameters: new object[] { _ntAuthentication.Instance });

        public byte[] Encrypt(byte[] input)
        {
            if (input == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(input));
            }

            byte[] _writeBuffer = new byte[4];
            int totalBytes = _ntAuthentication.Encrypt(input, ref _writeBuffer);
            byte[] result = new byte[totalBytes - 4];
            Buffer.BlockCopy(_writeBuffer, 4, result, 0, result.Length);
            return result;
        }

        public void Dispose() => _ntAuthentication.CloseContext();

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
