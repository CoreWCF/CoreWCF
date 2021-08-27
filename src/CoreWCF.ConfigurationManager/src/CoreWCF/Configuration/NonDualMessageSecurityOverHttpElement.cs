// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class NonDualMessageSecurityOverHttpElement : MessageSecurityOverHttpElement
    {
        [ConfigurationProperty(ConfigurationStrings.EstablishSecurityContext, DefaultValue = true)]
        public bool EstablishSecurityContext
        {
            get { return (bool)base[ConfigurationStrings.EstablishSecurityContext]; }
            set { base[ConfigurationStrings.EstablishSecurityContext] = value; }
        }
    }
}
