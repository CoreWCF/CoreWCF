// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Options controlling how <see cref="BindingHydrator"/> reads configuration.
    /// </summary>
    public sealed class BindingHydratorOptions
    {
        /// <summary>
        /// Resolves the type names configuration uses. Empty by default: nothing is discovered, so a type is named
        /// by its assembly qualified name unless the host registered a shorter one. See
        /// <see cref="ServiceModelTypeRegistry"/>.
        /// </summary>
        public ServiceModelTypeRegistry Registry { get; set; } = new ServiceModelTypeRegistry();

        /// <summary>
        /// The key naming the concrete type of a polymorphic value. Defaults to <c>Type</c>.
        /// </summary>
        public string DiscriminatorKey { get; set; } = "Type";
    }
}
