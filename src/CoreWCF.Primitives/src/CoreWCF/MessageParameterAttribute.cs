// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.Parameter, Inherited = false)]
    public sealed class MessageParameterAttribute : Attribute
    {
        private string _name;
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
    }
}