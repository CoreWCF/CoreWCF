using System;
using CoreWCF.Description;

namespace CoreWCF
{
    [AttributeUsage(ServiceModelAttributeTargets.ServiceContract, Inherited = false, AllowMultiple = false)]
    public sealed class ServiceContractAttribute : Attribute
    {
        Type _callbackContract;
        string _configurationName;
        string _name;
        string _ns;
        SessionMode _sessionMode;

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
                    NamingHelper.CheckUriProperty(value, "Namespace");
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

        public Type CallbackContract
        {
            get { return _callbackContract; }
            set { _callbackContract = value; }
        }

    }
}