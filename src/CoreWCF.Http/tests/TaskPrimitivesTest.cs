using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp

{
    public class TaskPrimitivesTest
    {
        public static DateTime TestDateTime = new DateTime(2010, 09, 04, new GregorianCalendar(GregorianCalendarTypes.USEnglish));
        private ITestOutputHelper _output;

        public TaskPrimitivesTest(ITestOutputHelper output)
        {
            _output = output;
        }

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

                    bool success = true;
                    if (!(((Task<Int32>)tasks[0]).Result.Equals(12600))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 12600, ((Task<Int32>)tasks[0]).Result); }
                    if (!(((Task<Byte>)tasks[1]).Result.Equals(124))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 124, ((Task<Byte>)tasks[1]).Result); }
                    if (!(((Task<SByte>)tasks[2]).Result.Equals(-124))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", -124, ((Task<SByte>)tasks[2]).Result); }
                    if (!(((Task<short>)tasks[3]).Result.Equals(567))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 567, ((Task<short>)tasks[3]).Result); }
                    if (!(((Task<ushort>)tasks[4]).Result.Equals(112))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 112, ((Task<ushort>)tasks[4]).Result); }
                    if (!(((Task<Double>)tasks[5]).Result.Equals(588.1200))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 588.1200, ((Task<Double>)tasks[5]).Result); }
                    if (!(((Task<UInt32>)tasks[6]).Result.Equals(12566))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 12566, ((Task<UInt32>)tasks[6]).Result); }
                    if (!(((Task<long>)tasks[7]).Result.Equals(12566))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 12566, ((Task<long>)tasks[7]).Result); }
                    if (!(((Task<ulong>)tasks[8]).Result.Equals(12566))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 12566, ((Task<ulong>)tasks[8]).Result); }
                    if (!(((Task<char>)tasks[9]).Result.Equals('r'))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 'r', ((Task<char>)tasks[9]).Result); }
                    if (!(((Task<bool>)tasks[10]).Result.Equals(true))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", true, ((Task<bool>)tasks[10]).Result); }
                    if (!(((Task<float>)tasks[11]).Result.Equals(12566))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 12566, ((Task<float>)tasks[11]).Result); }
                    if (!(((Task<Decimal>)tasks[12]).Result.Equals(12566.4565m))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 12566.4565m, ((Task<Decimal>)tasks[12]).Result); }
                    if (!(((Task<String>)tasks[13]).Result.Equals("Hello Seattle"))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", "Hello Seattle", ((Task<String>)tasks[13]).Result); }
                    if (!(((Task<DateTime>)tasks[14]).Result.Equals(TestDateTime))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", TestDateTime, ((Task<DateTime>)tasks[14]).Result); }
                    if (!(((Task<int[][]>)tasks[15]).Result.Length.Equals(2))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 2, ((Task<int[][]>)tasks[15]).Result.Length); }
                    if (!(((Task<float[]>)tasks[16]).Result.Length.Equals(20))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 20, ((Task<float[]>)tasks[16]).Result.Length); }
                    if (!(((Task<byte[]>)tasks[17]).Result.Length.Equals(10))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 10, ((Task<byte[]>)tasks[17]).Result.Length); }
                    if (!(((Task<int?>)tasks[18]).Result.Equals(100))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", 100, ((Task<int?>)tasks[18]).Result); }
                    if (!(((Task<TimeSpan>)tasks[19]).Result.ToString().Equals("00:00:05"))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", "00:00:05", ((Task<TimeSpan>)tasks[19]).Result.ToString()); }
                    if (!(((Task<Guid>)tasks[20]).Result.ToString().Equals("7a1c7e9a-f4ce-4861-852c-c05ec59fad4d"))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", "7a1c7e9a-f4ce-4861-852c-c05ec59fad4d", ((Task<Guid>)tasks[20]).Result.ToString()); }
                    if (!(((Task<Color>)tasks[21]).Result.Equals(Color.Blue))) { success = false; _output.WriteLine("Expected Response {0}, but got {1}", Color.Blue, ((Task<Color>)tasks[21]).Result); }

                    if (!success)
                    {
                        throw new Exception();
                    }
                }
                _output.WriteLine("Variation passed");          
        }

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
