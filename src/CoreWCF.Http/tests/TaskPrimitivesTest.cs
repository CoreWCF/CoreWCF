// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading.Tasks;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class TaskPrimitivesTest
    {
        private ITestOutputHelper _output;
        public static DateTime TestDateTime = new DateTime(2010, 09, 04, new GregorianCalendar(GregorianCalendarTypes.USEnglish));
        public TaskPrimitivesTest(ITestOutputHelper output)
        {
            _output = output;
        }

#if NET472
        // Unstable on NET472
#else
        [Fact]
        public void InvokeTaskBaseAsycn()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestPrimitives>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/TaskPrimitives/basichttp.svc")));
                var channel = factory.CreateChannel();

                Task[] tasks = new Task[22];
                tasks[0] = channel.GetInt();
                tasks[1] = channel.GetByte();
                tasks[2] = channel.GetSByte();
                tasks[3] = channel.GetShort();
                tasks[4] = channel.GetUShort();
                tasks[5] = channel.GetDouble();
                tasks[6] = channel.GetUInt();
                tasks[7] = channel.GetLong();
                tasks[8] = channel.GetULong();
                tasks[9] = channel.GetChar();
                tasks[10] = channel.GetBool();
                tasks[11] = channel.GetFloat();
                tasks[12] = channel.GetDecimal();
                tasks[13] = channel.GetString();
                tasks[14] = channel.GetDateTime();
                tasks[15] = channel.GetintArr2D();
                tasks[16] = channel.GetfloatArr();
                tasks[17] = channel.GetbyteArr();
                tasks[18] = channel.GetnullableInt();
                tasks[19] = channel.GetTimeSpan();
                tasks[20] = channel.GetGuid();
                tasks[21] = channel.GetEnum();
                Task.WaitAll(tasks);

                Assert.Equal(12600, ((Task<int>)tasks[0]).Result);
                Assert.Equal(124, ((Task<byte>)tasks[1]).Result);
                Assert.Equal(-124, ((Task<sbyte>)tasks[2]).Result);
                Assert.Equal(567, ((Task<short>)tasks[3]).Result);
                Assert.Equal(112, ((Task<ushort>)tasks[4]).Result);
                Assert.Equal(588.1200, ((Task<double>)tasks[5]).Result);
                Assert.Equal(12566, (double)((Task<uint>)tasks[6]).Result);
                Assert.Equal(12566, ((Task<long>)tasks[7]).Result);
                Assert.Equal(12566, (double)((Task<ulong>)tasks[8]).Result);
                Assert.Equal('r', ((Task<char>)tasks[9]).Result);
                Assert.True(((Task<bool>)tasks[10]).Result);
                Assert.Equal(12566, ((Task<float>)tasks[11]).Result);
                Assert.Equal(12566.4565m, ((Task<decimal>)tasks[12]).Result);
                Assert.Equal("Hello Seattle", ((Task<string>)tasks[13]).Result);
                Assert.Equal(TestDateTime, ((Task<DateTime>)tasks[14]).Result);
                Assert.Equal(2, ((Task<int[][]>)tasks[15]).Result.Length);
                Assert.Equal(20, ((Task<float[]>)tasks[16]).Result.Length);
                Assert.Equal(10, ((Task<byte[]>)tasks[17]).Result.Length);
                Assert.Equal(100, ((Task<int?>)tasks[18]).Result);
                Assert.Equal("00:00:05", ((Task<TimeSpan>)tasks[19]).Result.ToString());
                Assert.Equal("7a1c7e9a-f4ce-4861-852c-c05ec59fad4d", ((Task<Guid>)tasks[20]).Result.ToString());
                Assert.Equal(Color.Blue, ((Task<Color>)tasks[21]).Result);
            }
        }
#endif

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestPrimitives>();
                    builder.AddServiceEndpoint<Services.TestPrimitives, ServiceContract.ITestPrimitives>(new CoreWCF.BasicHttpBinding(), "/TaskPrimitives/basichttp.svc");
                });
            }
        }
    }
}
