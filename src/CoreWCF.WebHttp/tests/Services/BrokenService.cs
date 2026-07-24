// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ServiceContract;

namespace Services;


public class BrokenService : IBrokenServiceContract
{
    public DtoWithReference GetWithReference() => new DtoWithReference();

    public DtoWithCircularDependency GetCircularGraph()
    {
        var ret = new DtoWithCircularDependency { ReferenceTo = new DtoWithCircularDependency() };
        ret.ReferenceTo.ReferenceTo = ret;

        return ret;
    }
}
