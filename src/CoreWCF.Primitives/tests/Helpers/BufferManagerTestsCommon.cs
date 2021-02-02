// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using CoreWCF.Channels;

namespace CoreWCF.Primitives.Tests.Helpers
{
    public static class BufferManagerTestsCommon
    {
        private const string SynchronizedBufferPool = "synchronizedbufferpool";
        private const string LargeBufferPool = "largebufferpool";
        public static int TrainingCount = 64;
        public static int LargeBufferLimit = 85000;

        public static bool VerifyBufferPoolsCreated(BufferManager bufferManager)
        {
            bool flag = true;
            PropertyInfo property = bufferManager.GetType().GetProperty("InternalBufferManager");
            object value = property.GetValue(bufferManager, null);
            object obj = value.GetType().InvokeMember("_bufferPools", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField, null, value, null);
            Array array = obj as Array;
            for (int i = 0; i < array.Length; i++)
            {
                object value2 = array.GetValue(i);
                PropertyInfo property2 = value2.GetType().GetProperty("BufferSize");
                int num = (int)property2.GetValue(value2, null);
                string text = value2.GetType().ToString();
                if (num < LargeBufferLimit)
                {
                    flag &= text.ToLower().Contains(SynchronizedBufferPool);
                }
                else
                {
                    flag &= text.ToLower().Contains(LargeBufferPool);
                }
            }

            return flag;
        }
    }
}
