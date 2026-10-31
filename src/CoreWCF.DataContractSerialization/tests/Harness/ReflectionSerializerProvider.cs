// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Description;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// The reference implementation: CoreWCF's stock behavior, which produces a real
    /// reflection-based DataContractSerializer. This is the oracle the golden fixtures record.
    /// </summary>
    public sealed class ReflectionSerializerProvider : SerializerProvider
    {
        public override string Id => "Reflection";

        public override bool CanProduceFixtures => true;

        protected override DataContractSerializerOperationBehavior CreateBehavior(OperationDescription operation) =>
            new DataContractSerializerOperationBehavior(operation);
    }
}
