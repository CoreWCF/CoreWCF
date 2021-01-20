using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Security.NegotiateInternal
{
    enum NegotiateInternalSecurityStatusErrorCode
    {
        
        NotSet = 0,
        OK,
        ContinueNeeded,
        CompleteNeeded,
        CompAndContinue,
        ContextExpired,
        CredentialsNeeded,
        Renegotiate,

        // Errors
        OutOfMemory,
        InvalidHandle,
        Unsupported,
        TargetUnknown,
        InternalError,
        PackageNotFound,
        NotOwner,
        CannotInstall,
        InvalidToken,
        CannotPack,
        QopNotSupported,
        NoImpersonation,
        LogonDenied,
        UnknownCredentials,
        NoCredentials,
        MessageAltered,
        OutOfSequence,
        NoAuthenticatingAuthority,
        IncompleteMessage,
        IncompleteCredentials,
        BufferNotEnough,
        WrongPrincipal,
        TimeSkew,
        UntrustedRoot,
        IllegalMessage,
        CertUnknown,
        CertExpired,
        DecryptFailure,
        AlgorithmMismatch,
        SecurityQosFailed,
        SmartcardLogonRequired,
        UnsupportedPreauth,
        BadBinding,
        DowngradeDetected,
        ApplicationProtocolMismatch
    
}
}
