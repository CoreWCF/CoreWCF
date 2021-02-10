// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    // This class is a substitute for System.DBNull. It's only use with WCF is to allow HopperCache to tell the
    // difference between a non-existent value and a null value.
    public sealed class DBNull
    {
        //Package private constructor
        private DBNull()
        {
        }

        public static readonly DBNull Value = new DBNull();
    }
}