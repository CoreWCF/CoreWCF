using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Generic;
using System.Text;

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
