// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Minimal support types extracted from dotnet/runtime at bbfaee3bfa7edb0d556556bc32778d09a745134b.
//
// Primitives.cs is not self-contained: it references two symbols declared in upstream files that
// are otherwise out of scope for v1 (ObjRefSample.cs is IObjectReference/ISerializable territory,
// SampleTypes.cs is 163 KB of DataContractResolver-oriented material). Rather than import either
// wholesale, only the referenced declarations are reproduced here, verbatim and with provenance.
//
// When those files are eventually imported in full, delete the corresponding declaration here.

using System;
using System.Runtime.Serialization;

namespace SerializationTestTypes
{
    // Copied from https://github.com/dotnet/runtime/blob/bbfaee3bfa7edb0d556556bc32778d09a745134b/src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTestTypes/ObjRefSample.cs#L364
    // Upstream marker used by ComparisonHelper to skip a member during object-graph comparison.
    // It has no effect on serialization, and this corpus compares XML bytes rather than object
    // graphs, so it is carried purely to let Primitives.cs compile unmodified.
    public class IgnoreMemberAttribute : Attribute
    {
    }

    // Copied from https://github.com/dotnet/runtime/blob/bbfaee3bfa7edb0d556556bc32778d09a745134b/src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTestTypes/ObjRefSample.cs#L9
    // Referenced by InheritanceCases.cs. ObjRefSample.cs is not imported wholesale because it also
    // declares SerIser, an ISerializable type that is out of scope for v1.
    [DataContract(IsReference = true)]
    public class SimpleDC
    {
        [DataMember]
        public string Data;

        public SimpleDC() { }

        public SimpleDC(bool init)
        {
            Data = "This is a string";
        }
    }

    // Copied from https://github.com/dotnet/runtime/blob/bbfaee3bfa7edb0d556556bc32778d09a745134b/src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTestTypes/ObjRefSample.cs#L38
    // Referenced by InheritanceObjectRef.cs. Two members pointing at the same instance, so it
    // exercises z:Ref within a reference-preserving contract.
    [DataContract(IsReference = true)]
    public class SimpleDCWithRef
    {
        [DataMember]
        public SimpleDC Data;

        [DataMember]
        public SimpleDC RefData;

        public SimpleDCWithRef() { }

        public SimpleDCWithRef(bool init)
        {
            Data = new SimpleDC(true);
            RefData = Data;
        }
    }

    // Copied from https://github.com/dotnet/runtime/blob/bbfaee3bfa7edb0d556556bc32778d09a745134b/src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTestTypes/SampleTypes.cs#L5650
    [DataContract]
    public struct PublicDCStruct
    {
        [DataMember]
        public string Data;

        public PublicDCStruct(bool init)
        {
            Data = "Data";
        }
    }
}
