using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.ServiceModel.Channels.Framing
{
    internal abstract class FramingConnectionContext
    {
        private const string ViaKey = "connection.Via";

        private ConnectionContext _connection;

        protected FramingConnectionContext(ConnectionContext connection)
        {
            Connection = connection;
        }

        protected ConnectionContext Connection { get; }

        public Uri Via
        {
            get
            {
                return GetProperty<Uri>(ViaKey);
            }
            set
            {
                SetProperty<Uri>(ViaKey, value);
            }
        }

        protected T GetProperty<T>(string key)
        {
            return Connection.Items.TryGetValue(key, out object value) ? (T)value : default(T);
        }

        protected void SetProperty<T>(string key, T value)
        {
            Connection.Items[key] = value;
        }

        internal abstract Task CompleteHandshakeAsync();

    }
}