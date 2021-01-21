// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.OperationContract, AllowMultiple = true, Inherited = false)]
    public sealed class FaultContractAttribute : Attribute
    {
        private string action;
        private string name;
        private string ns;
        private readonly Type type;

        public FaultContractAttribute(Type detailType)
        {
            if (detailType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(detailType));
            }

            type = detailType;
        }

        public Type DetailType
        {
            get { return type; }
        }

        public string Action
        {
            get { return action; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                action = value;
            }
        }

        public string Name
        {
            get { return name; }
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

                name = value;
            }
        }

        public string Namespace
        {
            get { return ns; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    NamingHelper.CheckUriProperty(value, "Namespace");
                }

                ns = value;
            }
        }
    }
}