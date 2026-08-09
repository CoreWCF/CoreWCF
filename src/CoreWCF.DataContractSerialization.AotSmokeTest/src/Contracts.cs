// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF;
using CoreWCF.DataContractSerialization;

namespace CoreWCF.DataContractSerialization.AotSmokeTest
{
    /// <summary>
    /// The shapes worth carrying across the wire under AOT: a nested contract, a collection, an
    /// enum and a nullable, because each reaches different generated code.
    /// </summary>
    [DataContract(Namespace = "http://corewcf.example/aot")]
    public class Order
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Customer { get; set; }

        [DataMember]
        public OrderStatus Status { get; set; }

        [DataMember]
        public DateTime PlacedUtc { get; set; }

        [DataMember]
        public decimal? Discount { get; set; }

        [DataMember]
        public OrderLine Line { get; set; }

        [DataMember]
        public List<string> Tags { get; set; }
    }

    [DataContract(Namespace = "http://corewcf.example/aot")]
    public class OrderLine
    {
        [DataMember]
        public string Sku { get; set; }

        [DataMember]
        public int Quantity { get; set; }
    }

    [DataContract(Namespace = "http://corewcf.example/aot")]
    public enum OrderStatus
    {
        [EnumMember]
        Pending = 0,

        [EnumMember(Value = "in-progress")]
        InProgress = 1,

        [EnumMember]
        Shipped = 2
    }

    [ServiceContract(Namespace = "http://corewcf.example/aot")]
    public interface IOrderService
    {
        [OperationContract]
        Order Echo(Order order);
    }

    public class OrderService : IOrderService
    {
        public Order Echo(Order order)
        {
            // Returned rather than reflected back, so the response exercises the write path over a
            // graph the read path produced.
            return order;
        }
    }

    /// <summary>
    /// The contracts the generator emits serializers for. Under AOT this is the only thing standing
    /// between the service and a reflection-based serializer that cannot run.
    /// </summary>
    [DataContractSerializable(typeof(Order))]
    [DataContractSerializable(typeof(OrderLine))]
    public partial class OrderContracts : DataContractSerializerContext
    {
    }
}
