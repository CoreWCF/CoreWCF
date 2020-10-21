//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------
namespace Services
{
    using System;
    using System.Net.WebSockets;
    using System.Reflection;
    using System.Resources;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    public class Common
    {
        public static bool WebSocketNotSupported
        {
            get
            {
                return (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor < 2) || Environment.OSVersion.Version.Major < 6;
            }
        }

        public static string CreateInterestingString(int length)
        {
            char[] chars = new char[length];
            int index = 0;

            // Arrays of odd length will start with a single char.
            // The rest of the entries will be surrogate pairs.
            if (length % 2 == 1)
            {
                chars[index] = 'a';
                index++;
            }

            // Fill remaining entries with surrogate pairs
            int seed = DateTime.Now.Millisecond;
            Console.WriteLine("Seed for CreateInterestingCharArray = {0}", seed);
            Random rand = new Random(seed);
            char highSurrogate;
            char lowSurrogate;

            while (index < length)
            {
                highSurrogate = Convert.ToChar(rand.Next(0xD800, 0xDC00));
                lowSurrogate = Convert.ToChar(rand.Next(0xDC00, 0xE000));

                chars[index] = highSurrogate;
                ++index;
                chars[index] = lowSurrogate;
                ++index;
            }

            return new string(chars, 0, chars.Length);
        }
    }
}
