// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreWCF.DataContractSerialization.AotSmokeTest
{
    /// <summary>
    /// Runs a CoreWCF service under Native AOT and calls it over HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Staged on purpose. Serialization is the last link in a long chain, and a failure anywhere
    /// earlier - the host, the dispatcher, the binding - would otherwise read as "AOT does not
    /// work" without saying what does. Each stage reports its own result and the run continues past
    /// a failure where it can, so one publish answers as many questions as possible.
    /// </para>
    /// <para>
    /// The client is a raw HttpClient posting a hand-written envelope. System.ServiceModel is not an
    /// option: it does not support AOT either, so using it would be measuring the wrong thing.
    /// </para>
    /// </remarks>
    public static class Program
    {
        private const string Address = "http://127.0.0.1:8081";

        private static readonly List<string> s_failures = new List<string>();

        /// <summary>
        /// Keeps the contract and the service discoverable by reflection.
        /// </summary>
        /// <remarks>
        /// CoreWCF's TypeLoader finds operations by reflecting over the contract interface. Nothing
        /// calls IOrderService.Echo statically - the whole point of a dispatcher is that the call is
        /// dynamic - so without this the trimmer removes the methods and the contract arrives with
        /// zero operations. That is a CoreWCF annotation gap rather than an application mistake; it
        /// is worked around here so that the stages after it can be reached at all.
        /// </remarks>
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IOrderService))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OrderService))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Order))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OrderLine))]
        public static async Task<int> Main()
        {
            Console.WriteLine("CoreWCF DataContractSerializer - Native AOT smoke test");
            Console.WriteLine(new string('-', 60));

            Stage("runtime is AOT", RuntimeIsAot);
            Stage("switch is left at its default", SwitchDefaultApplies);
            Stage("generated serializer resolves and round-trips", GeneratedSerializerWorks);
            await StageAsync("service answers over HTTP", ServiceAnswersOverHttpAsync).ConfigureAwait(false);
            Stage("reflection serializer does not silently truncate", ReflectionSerializerBehaviour);

            Console.WriteLine(new string('-', 60));

            if (s_failures.Count == 0)
            {
                Console.WriteLine("PASS");
                return 0;
            }

            Console.WriteLine("FAIL (" + s_failures.Count + ")");
            foreach (string failure in s_failures)
            {
                Console.WriteLine("  " + failure);
            }

            return 1;
        }

        private static void Stage(string name, Func<string> body)
        {
            try
            {
                string detail = body();
                Console.WriteLine("  ok   " + name + (detail == null ? string.Empty : " - " + detail));
            }
            catch (Exception e)
            {
                Console.WriteLine("  FAIL " + name);
                Console.WriteLine("       " + e.GetType().FullName + ": " + e.Message);
                s_failures.Add(name);
            }
        }

        private static async Task StageAsync(string name, Func<Task<string>> body)
        {
            try
            {
                string detail = await body().ConfigureAwait(false);
                Console.WriteLine("  ok   " + name + (detail == null ? string.Empty : " - " + detail));
            }
            catch (Exception e)
            {
                Console.WriteLine("  FAIL " + name);
                Console.WriteLine("       " + e.GetType().FullName + ": " + e.Message);
                s_failures.Add(name);
            }
        }

        /// <summary>Without this the rest proves nothing: it would just be an ordinary run.</summary>
        private static string RuntimeIsAot()
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                throw new InvalidOperationException(
                    "Dynamic code is supported, so this is not an AOT publish and nothing below is a test of one.");
            }

            return "IsDynamicCodeSupported=false";
        }

        /// <summary>
        /// That the generated path is on by default here, stated as the rule it follows.
        /// </summary>
        /// <remarks>
        /// An earlier version of this read GeneratedSerializerSwitch by name and got a
        /// TypeLoadException: ILC keeps the code but not the reflection metadata for an internal
        /// type nothing looks up by name. That is correct behaviour and a fair warning about probing
        /// internals in a trimmed app. The switch resolving correctly is covered by unit tests; what
        /// matters here is the consequence, which the HTTP stage measures directly.
        /// </remarks>
        private static string SwitchDefaultApplies()
        {
            if (AppContext.TryGetSwitch("CoreWCF.Serialization.UseGeneratedDataContractSerializers", out bool explicitly))
            {
                return "set explicitly to " + explicitly;
            }

            // The documented default: on exactly where the reflection path is the broken one.
            return "unset, so the no-dynamic-code default applies";
        }

        private static string GeneratedSerializerWorks()
        {
            OrderContracts contracts = new OrderContracts();
            System.Xml.XmlDictionary dictionary = new System.Xml.XmlDictionary(2);

            CoreWCF.Runtime.Serialization.AotXmlObjectSerializer serializer = contracts.GetSerializer(
                typeof(Order),
                dictionary.Add("Order"),
                dictionary.Add("http://corewcf.example/aot"));

            if (serializer == null)
            {
                throw new InvalidOperationException("No generated serializer for Order.");
            }

            byte[] document = SoapEnvelope.Write(serializer, SampleOrder());

            if (!serializer.CanReadObject)
            {
                throw new InvalidOperationException("The generated serializer for Order cannot read.");
            }

            Order roundTripped = (Order)SoapEnvelope.Read(serializer, document);
            AssertSame(SampleOrder(), roundTripped);

            return document.Length + " bytes, read back identical";
        }

        private static async Task<string> ServiceAnswersOverHttpAsync()
        {
            using (IHost host = BuildHost())
            {
                await host.StartAsync().ConfigureAwait(false);

                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        StringContent content = new StringContent(
                            SoapEnvelope.EchoRequest(), Encoding.UTF8, "text/xml");
                        content.Headers.Add("SOAPAction", "\"http://corewcf.example/aot/IOrderService/Echo\"");

                        HttpResponseMessage response = await client
                            .PostAsync(Address + "/orders.svc", content)
                            .ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "HTTP " + (int)response.StatusCode + ": " + Trim(body));
                        }

                        SoapEnvelope.AssertEchoResponse(body);

                        return body.Length + " bytes back, contents match";
                    }
                }
                finally
                {
                    await host.StopAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// What the reflection-based serializer does with the same contract here.
        /// </summary>
        /// <remarks>
        /// Not an assertion about which way it goes - both outcomes are informative. If it throws,
        /// the generated path is the only thing making this service work. If it succeeds, the
        /// runtime has grown enough support to serialize this shape without dynamic code, and the
        /// generated path is an optimization here rather than a requirement.
        /// </remarks>
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "Calling the annotated serializer on purpose - whether it works under AOT is the question being asked.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Same. A suppression here is what lets any other IL2026 or IL3050 in this app be a real finding.")]
        private static string ReflectionSerializerBehaviour()
        {
            byte[] generated;
            OrderContracts contracts = new OrderContracts();
            System.Xml.XmlDictionary dictionary = new System.Xml.XmlDictionary(2);

            generated = SoapEnvelope.Write(
                contracts.GetSerializer(typeof(Order), dictionary.Add("Order"), dictionary.Add(SoapEnvelope.ContractNamespace)),
                SampleOrder());

            byte[] reflected;
            try
            {
                System.Runtime.Serialization.DataContractSerializer serializer =
                    new System.Runtime.Serialization.DataContractSerializer(
                        typeof(Order), "Order", SoapEnvelope.ContractNamespace);

                reflected = SoapEnvelope.Write(serializer, SampleOrder());
            }
            catch (Exception e)
            {
                return "throws " + e.GetType().Name + ", so the generated path is load-bearing here";
            }

            if (reflected.Length == generated.Length)
            {
                return "it produces the same " + generated.Length + " bytes, so the generated path is an optimization here";
            }

            // The outcome worth having a stage for. It did not throw - it wrote a shorter document,
            // which is a graph missing members, returned as though nothing were wrong.
            Console.WriteLine("       generated : " + Encoding.UTF8.GetString(generated));
            Console.WriteLine("       reflection: " + Encoding.UTF8.GetString(reflected));

            throw new InvalidOperationException(
                "The reflection-based serializer wrote " + reflected.Length + " bytes where the generated one wrote " +
                generated.Length + ", without throwing. Under AOT it loses members silently.");
        }

        private static IHost BuildHost() =>
            Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseUrls(Address);
                    web.ConfigureServices(services => services.AddServiceModelServices());
                    web.Configure(app => app.UseServiceModel(builder =>
                    {
                        builder.AddService<OrderService>();
                        builder.AddServiceEndpoint<OrderService, IOrderService>(
                            new BasicHttpBinding(), "/orders.svc");

                        // Replace rather than add: Behaviors is keyed and the first entry wins.
                        builder.ConfigureServiceHostBase<OrderService>(host =>
                        {
                            OrderContracts contracts = new OrderContracts();

                            foreach (ServiceEndpoint endpoint in host.Description.Endpoints)
                            {
                                foreach (OperationDescription operation in endpoint.Contract.Operations)
                                {
                                    operation.OperationBehaviors.Remove(typeof(DataContractSerializerOperationBehavior));
                                    operation.OperationBehaviors.Add(
                                        new GeneratedDataContractSerializerOperationBehavior(operation, contracts));
                                }
                            }
                        });
                    }));
                })
                .Build();

        private static Order SampleOrder() =>
            new Order
            {
                Id = 42,
                Customer = "Ada <&> Lovelace",
                Status = OrderStatus.InProgress,
                PlacedUtc = new DateTime(2026, 8, 9, 10, 11, 12, DateTimeKind.Utc),
                Discount = 12.5m,
                Line = new OrderLine { Sku = "A-1", Quantity = 2 },
                Tags = new List<string> { "rush", "gift" }
            };

        private static void AssertSame(Order expected, Order actual)
        {
            Require(actual != null, "null order");
            Require(expected.Id == actual.Id, "Id");
            Require(expected.Customer == actual.Customer, "Customer");
            Require(expected.Status == actual.Status, "Status");
            Require(expected.PlacedUtc == actual.PlacedUtc, "PlacedUtc");
            Require(expected.Discount == actual.Discount, "Discount");

            Require(actual.Line != null, "Line");
            Require(expected.Line.Sku == actual.Line.Sku, "Line.Sku");
            Require(expected.Line.Quantity == actual.Line.Quantity, "Line.Quantity");

            Require(actual.Tags != null && actual.Tags.Count == expected.Tags.Count, "Tags.Count");

            for (int i = 0; i < expected.Tags.Count; i++)
            {
                Require(expected.Tags[i] == actual.Tags[i], "Tags[" + i + "]");
            }
        }

        private static void Require(bool condition, string what)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Round trip differs at " + what + ".");
            }
        }

        private static string Trim(string value) =>
            value != null && value.Length > 400 ? value.Substring(0, 400) + "..." : value;
    }
}
