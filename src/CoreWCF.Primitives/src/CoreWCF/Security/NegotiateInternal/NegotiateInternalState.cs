﻿using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        private static readonly ConstructorInfo _constructor;
        private static readonly MethodInfo _getOutgoingBlob;
        private static readonly MethodInfo _isCompleted;
        private static readonly MethodInfo _isValidContext;

        private static readonly MethodInfo _protocol;
        private static readonly MethodInfo _getIdentity;
        private static readonly MethodInfo _closeContext;
        //private static readonly MethodInfo _getContext;
        private static readonly MethodInfo _encrypt;

        private static readonly FieldInfo _statusCode;
        private static readonly FieldInfo _statusException;
        private static readonly MethodInfo _getException;
        private static readonly FieldInfo _gssMinorStatus;
        private static readonly Type _gssExceptionType;

        private readonly object _instance;

        static NegotiateInternalState()
        {
            var secAssembly = typeof(AuthenticationException).Assembly;
            var ntAuthType = secAssembly.GetType("System.Net.NTAuthentication", throwOnError: true);
            _constructor = ntAuthType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
            _getOutgoingBlob = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("GetOutgoingBlob") && info.GetParameters().Count() == 3).Single();
            _isCompleted = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_IsCompleted")).Single();
            _protocol = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_ProtocolName")).Single();
            _closeContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("CloseContext")).Single();
           
            //Added these 2 new call
         //   _getContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
         //       info.Name.Equals("GetContext")).Single();
            _encrypt = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("Encrypt")).Single();
            _isValidContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
            info.Name.Equals("get_IsValidContext")).Single();
            //end 

            //TODO this fails in framework
            var securityStatusType = secAssembly.GetType("System.Net.SecurityStatusPal", throwOnError: true);
           // securityStatusType.get
            _statusCode = securityStatusType.GetField("ErrorCode");
            _statusException = securityStatusType.GetField("Exception");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var interopType = secAssembly.GetType("Interop", throwOnError: true);
                var netNativeType = interopType.GetNestedType("NetSecurityNative", BindingFlags.NonPublic | BindingFlags.Static);
                _gssExceptionType = netNativeType.GetNestedType("GssApiException", BindingFlags.NonPublic);
                _gssMinorStatus = _gssExceptionType.GetField("_minorStatus", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            var negoStreamPalType = secAssembly.GetType("System.Net.Security.NegotiateStreamPal", throwOnError: true);
            _getIdentity = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("GetIdentity")).Single();
            _getException = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("CreateExceptionFromError")).Single();
        }

        public NegotiateInternalState()
        {
           
                var credential = CredentialCache.DefaultCredentials;
                _instance = _constructor.Invoke(new object[] { true, "Negotiate", credential, null, 0, null });
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
                var parameters = new object[] { incomingBlob, false, null };
                var blob = (byte[])_getOutgoingBlob.Invoke(_instance, parameters);

                var securityStatus = parameters[2];
                // TODO: Update after corefx changes
                error = (Exception)(_statusException.GetValue(securityStatus)
                    ?? _getException.Invoke(null, new[] { securityStatus }));
                var errorCode = (NegotiateInternalSecurityStatusErrorCode)_statusCode.GetValue(securityStatus);

                // TODO: Remove after corefx changes
                // The linux implementation always uses InternalError;
                if (errorCode == NegotiateInternalSecurityStatusErrorCode.InternalError
                    && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && _gssExceptionType.IsInstanceOfType(error))
                {
                    var majorStatus = (uint)error.HResult;
                    var minorStatus = (uint)_gssMinorStatus.GetValue(error);

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

        public bool IsCompleted
        {
            get => (bool)_isCompleted.Invoke(_instance, Array.Empty<object>());
        }

        public string Protocol
        {
            get => (string)_protocol.Invoke(_instance, Array.Empty<object>());
        }

        public bool IsValidContext
        {
            get => (bool)_isValidContext.Invoke(_instance, Array.Empty<object>());
        }

        public IIdentity GetIdentity()
        {
            return (IIdentity)_getIdentity.Invoke(obj: null, parameters: new object[] { _instance });
        }

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("input");
            byte[] _writeBuffer = new byte[4];
            var parameters = new object[] { input, 0, input.Length , _writeBuffer, 0U};
          int totalBytes = (int)  _encrypt.Invoke(_instance, parameters);
            byte[] result = new byte[totalBytes-4];
            Buffer.BlockCopy((byte[])parameters[3], 4, result, 0, result.Length);
            return result;
        }

        public void Dispose()
        {
            _closeContext.Invoke(_instance, Array.Empty<object>());
        }

        private bool IsCredentialError(NegotiateInternalSecurityStatusErrorCode error)
        {
            return error == NegotiateInternalSecurityStatusErrorCode.LogonDenied ||
                error == NegotiateInternalSecurityStatusErrorCode.UnknownCredentials ||
                error == NegotiateInternalSecurityStatusErrorCode.NoImpersonation ||
                error == NegotiateInternalSecurityStatusErrorCode.NoAuthenticatingAuthority ||
                error == NegotiateInternalSecurityStatusErrorCode.UntrustedRoot ||
                error == NegotiateInternalSecurityStatusErrorCode.CertExpired ||
                error == NegotiateInternalSecurityStatusErrorCode.SmartcardLogonRequired ||
                error == NegotiateInternalSecurityStatusErrorCode.BadBinding;
        }

        private bool IsClientError(NegotiateInternalSecurityStatusErrorCode error)
        {
            return error == NegotiateInternalSecurityStatusErrorCode.InvalidToken ||
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
}