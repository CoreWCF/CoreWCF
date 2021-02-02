﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;

namespace CoreWCF
{
    public abstract class MessageContractMemberAttribute : Attribute
    {
        private string _name;
        private string _ns;

        //ProtectionLevel protectionLevel = ProtectionLevel.None;
        //bool hasProtectionLevel = false;

        internal const string NamespacePropertyName = "Namespace";
        public string Namespace
        {
            get { return _ns; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.Length > 0)
                {
                    NamingHelper.CheckUriProperty(value, "Namespace");
                }
                _ns = value;
                IsNamespaceSetExplicit = true;
            }
        }

        internal bool IsNamespaceSetExplicit { get; private set; }

        internal const string NamePropertyName = "Name";
        public string Name
        {
            get { return _name; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SR.SFxNameCannotBeEmpty));
                }

                _name = value; IsNameSetExplicit = true;
            }
        }

        internal bool IsNameSetExplicit { get; private set; }

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
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        this.protectionLevel = value;
        //        this.hasProtectionLevel = true;
        //    }
        //}

        //public bool HasProtectionLevel
        //{
        //    get { return this.hasProtectionLevel; }
        //}
    }
}