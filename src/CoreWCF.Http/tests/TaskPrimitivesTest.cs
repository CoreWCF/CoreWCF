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
        private readonly ITestOutputHelper _output;
        public static DateTime TestDateTime = new DateTime(2010, 09, 04, new GregorianCalendar(GregorianCalendarTypes.USEnglish));
        public TaskPrimitivesTest(ITestOutputHelper output)
        {
            _output = output;
        }

#if NET472
        // Unstable on NET472
#else
        [Fact]
        public async Task InvokeTaskBaseAsycn()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestPrimitives>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/TaskPrimitives/basichttp.svc")));
                ITestPrimitives channel = factory.CreateChannel();

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
                await Task.WhenAll(tasks);

                Assert.Equal(12600, await (Task<int>)tasks[0]);
                Assert.Equal(124, await (Task<byte>)tasks[1]);
                Assert.Equal(-124, await (Task<sbyte>)tasks[2]);
                Assert.Equal(567, await (Task<short>)tasks[3]);
                Assert.Equal(112, await (Task<ushort>)tasks[4]);
                Assert.Equal(588.1200, await (Task<double>)tasks[5]);
                Assert.Equal(12566, (double)await (Task<uint>)tasks[6]);
                Assert.Equal(12566, await (Task<long>)tasks[7]);
                Assert.Equal(12566, (double)await (Task<ulong>)tasks[8]);
                Assert.Equal('r', await (Task<char>)tasks[9]);
                Assert.True(await (Task<bool>)tasks[10]);
                Assert.Equal(12566, await (Task<float>)tasks[11]);
                Assert.Equal(12566.4565m, await (Task<decimal>)tasks[12]);
                Assert.Equal("Hello Seattle", await (Task<string>)tasks[13]);
                Assert.Equal(TestDateTime, await (Task<DateTime>)tasks[14]);
                var intArrays = await (Task<int[][]>)tasks[15];
                Assert.Equal(2, intArrays.Length);
                var floatArray = await (Task<float[]>)tasks[16];
                Assert.Equal(20, floatArray.Length);
                var byteArray = await (Task<byte[]>)tasks[17];
                Assert.Equal(10, byteArray.Length);
                Assert.Equal(100, await (Task<int?>)tasks[18]);
                var timeSpan = await (Task<TimeSpan>)tasks[19];
                Assert.Equal("00:00:05", timeSpan.ToString());
                var guid = await (Task<Guid>)tasks[20];
                Assert.Equal("7a1c7e9a-f4ce-4861-852c-c05ec59fad4d", guid.ToString());
                Assert.Equal(Color.Blue, await (Task<Color>)tasks[21]);
            }
        }
#endif

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
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
