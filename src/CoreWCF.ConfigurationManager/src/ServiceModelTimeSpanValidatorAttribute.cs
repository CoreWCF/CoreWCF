// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Configuration
{
    [AttributeUsage(AttributeTargets.Property)]
    sealed class ServiceModelTimeSpanValidatorAttribute : ConfigurationValidatorAttribute
    {
        TimeSpanValidatorAttribute innerValidatorAttribute;

        public ServiceModelTimeSpanValidatorAttribute()
        {
            this.innerValidatorAttribute = new TimeSpanValidatorAttribute();
            this.innerValidatorAttribute.MaxValueString = TimeoutHelper.MaxWait.ToString();
        }

        public override ConfigurationValidatorBase ValidatorInstance
        {
            get
            {
                return new TimeSpanOrInfiniteValidator(MinValue, MaxValue);
            }
        }

        public TimeSpan MinValue
        {
            get
            {
                return this.innerValidatorAttribute.MinValue;
            }
        }

        public string MinValueString
        {
            get
            {
                return this.innerValidatorAttribute.MinValueString;
            }
            set
            {
                this.innerValidatorAttribute.MinValueString = value;
            }
        }

        public TimeSpan MaxValue
        {
            get
            {
                return this.innerValidatorAttribute.MaxValue;
            }
        }

        public string MaxValueString
        {
            get
            {
                return this.innerValidatorAttribute.MaxValueString;
            }
            set
            {
                this.innerValidatorAttribute.MaxValueString = value;
            }
        }
    }
}
