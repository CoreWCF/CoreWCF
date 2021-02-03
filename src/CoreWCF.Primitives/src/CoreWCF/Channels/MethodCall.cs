// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Contracts;
using System.Reflection;

namespace CoreWCF.Channels
{
    // MethodCall associates a MethodBase with the arguments to pass to it.
    internal class MethodCall
    {
        public MethodCall(object[] args)
        {
            Contract.Assert(args != null);
            Args = args;
        }

        public MethodCall(MethodBase methodBase, object[] args) : this(args)
        {
            Contract.Assert(methodBase != null);
            MethodBase = methodBase;
        }

        public MethodBase MethodBase { get; private set; }

        public object[] Args { get; private set; }
    }
}