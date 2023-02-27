// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using RabbitMQ.Client;

namespace CoreWCF.ServiceModel.Channels
{
    public class RabbitMqConnectionSettings
    {
        private const string TempQueuePrefix = "corewcf-temp-";
        private const string AMQPScheme = "amqp";
        private const string SecureAMQPScheme = "amqps";
        private const string DefaultVirtualHost = "/";

        public Uri BaseAddress { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Exchange { get; set; } = string.Empty;
        public string QueueName { get; set; }
        public string VirtualHost { get; set; } = DefaultVirtualHost;
        public virtual string RoutingKey { get; set; }
        public string UserName { get; set; } = ConnectionFactory.DefaultUser;
        public string Password { get; set; } = ConnectionFactory.DefaultPass;
        public SslOption SslOption { get; set; }
        public bool AutomaticRecoveryEnabled => true;

        public static RabbitMqConnectionSettings FromUri(
            Uri uri,
            string userName,
            string password,
            SslOption sslOption = null,
            string virtualHost = DefaultVirtualHost)
        {
            if (uri == null)
            {
                throw new ArgumentException($"Parameter {nameof(uri)} cannot be null.");
            }
            
            sslOption = ConfigureSslOption(sslOption, uri);
            var queueName = GetQueueNameFromUri(uri);
            var exchange = GetExchangeFromUri(uri);
            var routingKey = GetRoutingKeyFromUri(uri, queueName);

            return new RabbitMqConnectionSettings
            {
                BaseAddress = uri,
                Host = uri.Host,
                Port = uri.Port,
                Exchange = exchange,
                RoutingKey = routingKey,
                QueueName = queueName,
                VirtualHost = virtualHost,
                UserName = userName,
                Password = password,
                SslOption = sslOption
            };
        }

        public ConnectionFactory GetConnectionFactory(string userName = null, string password = null)
        {
            return new ConnectionFactory
            {
                HostName = Host,
                Port = Port,
                VirtualHost = VirtualHost,
                UserName = userName ?? UserName,
                Password = password ?? Password,
                Ssl = SslOption,
                AutomaticRecoveryEnabled = AutomaticRecoveryEnabled
            };
        }

        private static string GetQueueNameFromUri(Uri uri)
        {
            if (uri.Segments.Length < 2)
            {
                throw new ArgumentException(SR.Format(SR.InvalidRabbitMqUri, uri));
            }

            var lastSegment = uri.Segments.LastOrDefault();
            if (lastSegment.EndsWith("/"))
            {
                // Exchange found but no queueName, so create unique queueName
                return $"{TempQueuePrefix}{new Guid()}";
            }

            return lastSegment.Replace("/", string.Empty);
        }

        private static string GetExchangeFromUri(Uri uri)
        {
            if (uri.Segments.Length < 2)
            {
                throw new ArgumentException(SR.Format(SR.InvalidRabbitMqUri, uri));
            }

            var secondSegment = uri.Segments[1];
            if (secondSegment.EndsWith("/"))
            {
                // Exchange name was found
                return secondSegment.Replace("/", string.Empty);
            }

            // Exchange name was not found
            return string.Empty;
        }

        private static string GetRoutingKeyFromUri(Uri uri, string queueName)
        {
            if (string.IsNullOrEmpty(uri.Fragment))
            {
                return queueName;
            }

            return uri.Fragment.Replace("#", string.Empty);
        }


        private static SslOption ConfigureSslOption(SslOption sslOption, Uri uri)
        {
            if (sslOption != null)
            {
                return sslOption;
            }

            if (uri.Scheme.Contains(SecureAMQPScheme))
            {
                return new SslOption { ServerName = uri.Host, Enabled = true };
            }

            return new SslOption();
        }
    }
}
