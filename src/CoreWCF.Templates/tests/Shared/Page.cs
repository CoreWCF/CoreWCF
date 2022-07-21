// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Templates.Test.Helpers;

public class Page
{
    public string Url { get; set; }
    public IEnumerable<string> Links { get; set; }
}
