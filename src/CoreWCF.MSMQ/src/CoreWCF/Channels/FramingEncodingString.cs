// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace CoreWCF.Channels
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class FramingEncodingString
    {
        public const string Soap11Utf8 = "text/xml; charset=utf-8";
        public const string Soap11Utf16 = "text/xml; charset=utf16";
        public const string Soap11Utf16FFFE = "text/xml; charset=unicodeFFFE";
        public const string Soap12Utf8 = "application/soap+xml; charset=utf-8";
        public const string Soap12Utf16 = "application/soap+xml; charset=utf16";
        public const string Soap12Utf16FFFE = "application/soap+xml; charset=unicodeFFFE";
        public const string MTOM = "multipart/related";
        public const string Binary = "application/soap+msbin1";
        public const string BinarySession = "application/soap+msbinsession1";
        public const string ExtendedBinaryGZip = Binary + "+gzip";
        public const string ExtendedBinarySessionGZip = BinarySession + "+gzip";
        public const string ExtendedBinaryDeflate = Binary + "+deflate";
        public const string ExtendedBinarySessionDeflate = BinarySession + "+deflate";
        public const string NamespaceUri = "http://schemas.microsoft.com/ws/2006/05/framing";
        private const string FaultBaseUri = NamespaceUri + "/faults/";
        public const string ContentTypeInvalidFault = FaultBaseUri + "ContentTypeInvalid";
        public const string ContentTypeTooLongFault = FaultBaseUri + "ContentTypeTooLong";
        public const string ConnectionDispatchFailedFault = FaultBaseUri + "ConnectionDispatchFailed";
        public const string EndpointNotFoundFault = FaultBaseUri + "EndpointNotFound";
        public const string EndpointUnavailableFault = FaultBaseUri + "EndpointUnavailable";
        public const string MaxMessageSizeExceededFault = FaultBaseUri + "MaxMessageSizeExceededFault";
        public const string ServerTooBusyFault = FaultBaseUri + "ServerTooBusy";
        public const string ServiceActivationFailedFault = FaultBaseUri + "ServiceActivationFailed";
        public const string UnsupportedModeFault = FaultBaseUri + "UnsupportedMode";
        public const string UnsupportedVersionFault = FaultBaseUri + "UnsupportedVersion";
        public const string UpgradeInvalidFault = FaultBaseUri + "UpgradeInvalid";
        public const string ViaTooLongFault = FaultBaseUri + "ViaTooLong";

        private const string ExceptionKey = "FramingEncodingString";
        public static bool TryGetFaultString(Exception exception, out string framingFault)
        {
            framingFault = null;
            if (exception.Data.Contains(ExceptionKey))
            {
                framingFault = exception.Data[ExceptionKey] as string;
                if (framingFault != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static void AddFaultString(Exception exception, string framingFault)
        {
            exception.Data[ExceptionKey] = framingFault;
        }
    }
}
