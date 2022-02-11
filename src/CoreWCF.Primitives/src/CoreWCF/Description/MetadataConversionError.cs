// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public class MetadataConversionError
    {
        public MetadataConversionError(string message) : this(message, false) { }

        public MetadataConversionError(string message, bool isWarning)
        {
            Message = message ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            IsWarning = isWarning;
        }

        public string Message { get; }

        public bool IsWarning { get; }

        public override bool Equals(object obj)
        {
            MetadataConversionError otherError = obj as MetadataConversionError;
            if (otherError == null)
                return false;
            return otherError.IsWarning == IsWarning && otherError.Message == Message;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }
    }
}
