﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    public sealed class MessageHeaderArrayAttribute : MessageHeaderAttribute
    {
    }
}
