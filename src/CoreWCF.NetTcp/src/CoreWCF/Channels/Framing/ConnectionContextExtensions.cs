// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Connections;

namespace CoreWCF.Channels.Framing
{
    internal static class ConnectionContextExtensions
    {
        public static void Set<T>(this ConnectionContext context, T value)
        {
            if (context.Items.ContainsKey(typeof(T)))
            {
                throw new ArgumentException(nameof(T));
            }

            context.Items[typeof(T)] = value;
        }

        public static void Replace<T>(this ConnectionContext context, T value)
        {
            if (!context.Items.ContainsKey(typeof(T)))
            {
                throw new ArgumentException(nameof(T));
            }

            context.Items[typeof(T)] = value;
        }

        public static T Get<T>(this ConnectionContext context)
        {
            if (context.Items.TryGetValue(typeof(T), out object item))
            {
                if (item is T) return (T)item;
            }

            return default(T);
        }
    }
}
