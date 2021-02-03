// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract, Inherited = false, AllowMultiple = false)]
    public sealed class ServiceContractAttribute : Attribute
    {
        private string _configurationName;
        private string _name;
        private string _ns;
        private SessionMode _sessionMode;

        public string ConfigurationName
        {
            get { return _configurationName; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                if (value == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SR.SFxConfigurationNameCannotBeEmpty));
                }
                _configurationName = value;
            }
        }

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
                _name = value;
            }
        }

        public string Namespace
        {
            get { return _ns; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    NamingHelper.CheckUriProperty(value, "Namespace");
                }

                _ns = value;
            }
        }

        public SessionMode SessionMode
        {
            get { return _sessionMode; }
            set
            {
                if (!SessionModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _sessionMode = value;
            }
        }

        public Type CallbackContract { get; set; }
    }
}