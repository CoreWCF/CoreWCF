// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using CoreWCF;

namespace Services
{
    [ServiceBehavior]
    public class ByRefService : ServiceContract.IByRefService
    {
        public static readonly Guid GuidA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid GuidB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private static volatile int s_number;

        public void GetOutParam(string str, out Guid result, bool option)
        {
            result = option ? GuidA : GuidB;
        }

        public bool ExchangeRefParam(ref Guid result)
        {
            if (result == GuidA)
            {
                result = GuidB;
                return true;
            }

            if (result == GuidB)
            {
                result = GuidA;
                return true;
            }

            return false;
        }

        public void SelectParam(string input, bool selection, ref string optionA, out string optionB)
        {
            if (selection)
            {
                optionA = input;
                optionB = null;
            }
            else
            {
                optionB = input;
            }
        }

        public void SetNumber(int number)
        {
            s_number = number;
        }

        public void SetNumberIn([In] int number)
        {
            s_number = number;
        }

        public void GetNumber(out int number)
        {
            number = s_number;
        }
    }
}
