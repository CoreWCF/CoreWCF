// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Kafka.Tests.Helpers;

public static class SslHelper
{
    public static string GetSslCaLocation()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "ca-root.crt");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return fileInfo.FullName;
    }

    public static string GetSslCaPem()
    {
        return File.ReadAllText(GetSslCaLocation());
    }

    public static string GetConsumerSslCertificateLocation()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "consumer.crt");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return fileInfo.FullName;
    }

    public static string GetConsumerSslCertificatePem()
    {
        return File.ReadAllText(GetConsumerSslCertificateLocation());
    }

    public static string GetConsumerSslKeyLocation()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "consumer.key");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return fileInfo.FullName;
    }

    public static string GetConsumerSslKeyPem()
    {
        return File.ReadAllText(GetConsumerSslKeyLocation());
    }

    public static string GetConsumerSslKeyPassword()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "consumer.key.cred");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return File.ReadAllText(fileInfo.FullName);
    }

    public static string GetProducerSslCertificateLocation()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "producer.crt");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return fileInfo.FullName;
    }

    public static string GetProducerSslCertificatePem()
    {
        return File.ReadAllText(GetProducerSslCertificateLocation());
    }

    public static string GetProducerSslKeyLocation()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "producer.key");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return fileInfo.FullName;
    }

    public static string GetProducerSslKeyPem()
    {
        return File.ReadAllText(GetProducerSslKeyLocation());
    }

    public static string GetProducerSslKeyPassword()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "src", "CoreWCF.Kafka", "tests", "kafka-secrets", "producer.key.cred");
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }
        return File.ReadAllText(fileInfo.FullName);
    }
}
