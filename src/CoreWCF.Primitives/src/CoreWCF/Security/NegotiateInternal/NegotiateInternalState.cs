// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Principal;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NegotiateInternalState : INegotiateInternalState
    {
        private readonly INTAuthenticationFacade _ntAuthentication;

        public NegotiateInternalState()
        {
            _ntAuthentication = NTAuthenticationFacade.Build();
        }

        public byte[] GetOutgoingBlob(byte[] incomingBlob, out BlobErrorType status, out Exception error)
        {
            try
            {
                byte[] blob = _ntAuthentication.GetOutgoingBlob(incomingBlob, out var securityStatus);

                var errorCode = securityStatus.ErrorCode;

                error = securityStatus.Exception;

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

        public IIdentity GetIdentity() => _ntAuthentication.GetIdentity();

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

        public void Dispose() => _ntAuthentication.Dispose();

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
