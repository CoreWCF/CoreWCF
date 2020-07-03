﻿using CoreWCF.Channels;
using System;
using System.Reflection;

namespace CoreWCF.Primitives.Tests.Helpers
{
    public class BufferManagerTestsCommon
    {
        private static string SynchronizedBufferPool = "synchronizedbufferpool";
        private static string LargeBufferPool = "largebufferpool";
        public static int TrainingCount = 64;
        public static int LargeBufferLimit = 85000;

        public static bool VerifyBufferPoolsCreated(BufferManager bufferManager)
        {
            bool flag = true;
            PropertyInfo property = bufferManager.GetType().GetProperty("InternalBufferManager");
            object value = property.GetValue(bufferManager, null);
            object obj = value.GetType().InvokeMember("bufferPools", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField, null, value, null);
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
