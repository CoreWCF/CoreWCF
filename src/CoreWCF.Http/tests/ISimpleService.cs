﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF;

[ServiceContract]
internal interface ISimpleService
{
    [OperationContract]
    string Echo(string echo);
}
