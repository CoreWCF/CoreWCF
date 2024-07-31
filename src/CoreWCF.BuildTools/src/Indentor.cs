// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.BuildTools;

internal sealed class Indentor
{
    const string ____ = "    ";
    const string ________ = "        ";
    const string ____________ = "            ";
    const string ________________ = "                ";
    const string ____________________ = "                    ";
    const string ________________________ = "                        ";
    const string ____________________________ = "                            ";
    const string ________________________________ = "                                ";
    public int Level { get; private set; } = 0;
    public void Increment()
    {
        Level++;
    }

    public void Decrement()
    {
        Level--;
    }

    public override string ToString() => Level switch
    {
        0 => string.Empty,
        1 => ____,
        2 => ________,
        3 => ____________,
        4 => ________________,
        5 => ____________________,
        6 => ________________________,
        7 => ____________________________,
        8 => ________________________________,
        _ => throw new InvalidOperationException()
    };
}
