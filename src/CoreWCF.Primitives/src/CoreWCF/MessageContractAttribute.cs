// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageContract, AllowMultiple = false)]
    public sealed class MessageContractAttribute : Attribute
    {
        private string wrappedName;
        private string wrappedNs;
        //ProtectionLevel protectionLevel = ProtectionLevel.None;
        //bool hasProtectionLevel = false;

        //internal const string ProtectionLevelPropertyName = "ProtectionLevel";
        //public ProtectionLevel ProtectionLevel
        //{
        //    get
        //    {
        //        return this.protectionLevel;
        //    }
        //    set
        //    {
        //        if (!ProtectionLevelHelper.IsDefined(value))
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
        //        this.protectionLevel = value;
        //        this.hasProtectionLevel = true;
        //    }
        //}

        //public bool HasProtectionLevel
        //{
        //    get { return this.hasProtectionLevel; }
        //}

        public bool IsWrapped { get; set; } = true;

        public string WrapperName
        {
            get
            {
                return wrappedName;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SR.SFxWrapperNameCannotBeEmpty));
                }

                wrappedName = value;
            }
        }

        public string WrapperNamespace
        {
            get
            {
                return wrappedNs;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    NamingHelper.CheckUriProperty(value, "WrapperNamespace");
                }

                wrappedNs = value;
            }
        }
    }

}