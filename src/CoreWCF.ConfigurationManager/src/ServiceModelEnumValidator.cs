// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Configuration;
using System.Reflection;

namespace CoreWCF.Configuration
{
    internal class ServiceModelEnumValidator : ConfigurationValidatorBase
    {
        Type enumHelperType;
        MethodInfo isDefined;

        public ServiceModelEnumValidator(Type enumHelperType)
        {
            this.enumHelperType = enumHelperType;
            this.isDefined = this.enumHelperType.GetMethod("IsDefined", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public override bool CanValidate(Type type)
        {
            return (this.isDefined != null);
        }

        public override void Validate(object value)
        {
            bool retVal = (bool)this.isDefined.Invoke(null, new object[] { value });

            if (!retVal)
            {
                ParameterInfo[] isDefinedParameters = this.isDefined.GetParameters();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value, isDefinedParameters[0].ParameterType));
            }
        }
    }
}
