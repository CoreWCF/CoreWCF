// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    // All implementations of this interface are required to be thread-safe

    // TODO: Either this interface needs to be public or it needs to be in some internal shared contract
    public interface IRequestReplyCorrelator
    {
        // throws if another object of the same type has been added for the same message
        // null is not a valid value for state.
        void Add<T>(Message request, T state);

        // returns null if no state is found.
        T Find<T>(Message reply, bool remove);
    }
}