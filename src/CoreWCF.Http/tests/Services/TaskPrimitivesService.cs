// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class TestPrimitives : ITestPrimitives
    {
        public Task<int> GetInt()
        {
            Task<int> task = new Task<int>(() =>
            {
                return 12566 + 34;
            });
            task.Start();
            return task;
        }

        public Task<byte> GetByte()
        {
            Task<byte> task = new Task<byte>(() =>
            {
                return 120 + 4;
            });
            task.Start();
            return task;
        }

        public Task<sbyte> GetSByte()
        {
            Task<sbyte> task = new Task<sbyte>(() =>
            {
                return -120 - 4;
            });
            task.Start();
            return task;
        }

        public Task<short> GetShort()
        {
            Task<short> task = new Task<short>(() =>
            {
                return 566 + 1;
            });
            task.Start();
            return task;
        }

        public Task<ushort> GetUShort()
        {
            Task<ushort> task = new Task<ushort>(() =>
            {
                return 111 + 1;
            });
            task.Start();
            return task;
        }

        public Task<double> GetDouble()
        {
            Task<double> task = new Task<Double>(() =>
            {
                return 588.1200;
            });
            task.Start();
            return task;
        }

        public Task<UInt32> GetUInt()
        {
            Task<UInt32> task = new Task<UInt32>(() =>
            {
                return 12566;
            });
            task.Start();
            return task;
        }

        public Task<long> GetLong()
        {
            Task<long> task = new Task<long>(() =>
            {
                return 12566;
            });
            task.Start();
            return task;
        }

        public Task<ulong> GetULong()
        {
            Task<ulong> task = new Task<ulong>(() =>
            {
                return 12566;
            });
            task.Start();
            return task;
        }

        public Task<char> GetChar()
        {
            Task<char> task = new Task<char>(() =>
            {
                return 'r';
            });
            task.Start();
            return task;
        }

        public Task<bool> GetBool()
        {
            Task<bool> task = new Task<bool>(() =>
            {
                return true;
            });
            task.Start();
            return task;
        }

        public Task<float> GetFloat()
        {
            Task<float> task = new Task<float>(() =>
            {
                return 12566;
            });
            task.Start();
            return task;
        }

        public Task<decimal> GetDecimal()
        {
            Task<decimal> task = new Task<decimal>(() =>
            {
                return 12566.4565m;
            });
            task.Start();
            return task;
        }

        public Task<string> GetString()
        {
            Task<String> task = new Task<String>(() =>
            {
                return "Hello Seattle";
            });
            task.Start();
            return task;
        }

        public Task<DateTime> GetDateTime()
        {
            Task<DateTime> task = new Task<DateTime>(() =>
            {
                return AsyncNetAdoptionConstants.TestDateTime;
            });
            task.Start();
            return task;
        }

        public Task<int[][]> GetintArr2D()
        {
            Task<int[][]> task = new Task<int[][]>(() =>
            {
                return new int[2][] { new int[] { 2, 3, 4 }, new int[] { 5, 6, 7, 8, 9 } };
            });
            task.Start();
            return task;
        }

        public Task<float[]> GetfloatArr()
        {
            Task<float[]> task = new Task<float[]>(() =>
            {
                return new float[20];
            });
            task.Start();
            return task;
        }

        public Task<byte[]> GetbyteArr()
        {
            Task<byte[]> task = new Task<byte[]>(() =>
            {
                return new byte[10];
            });
            task.Start();
            return task;
        }

        public Task<int?> GetnullableInt()
        {
            Task<int?> task = new Task<int?>(() =>
            {
                return 100;
            });
            task.Start();
            return task;
        }

        public Task<TimeSpan> GetTimeSpan()
        {
            Task<TimeSpan> task = new Task<TimeSpan>(() =>
            {
                return TimeSpan.FromSeconds(5);
            });
            task.Start();
            return task;
        }

        public Task<Guid> GetGuid()
        {
            Task<Guid> task = new Task<Guid>(() =>
            {
                return new Guid("7a1c7e9a-f4ce-4861-852c-c05ec59fad4d");
            });
            task.Start();
            return task;
        }

        public Task<Color> GetEnum()
        {
            Task<Color> task = new Task<Color>(() =>
            {
                return Color.Blue;
            });
            task.Start();
            return task;
        }
    }
}
