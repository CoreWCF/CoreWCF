// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Description;
using Xunit;

namespace DependencyInjection
{
    public class ServiceTypeErrorMessageTests
    {
        [Fact]
        public static void CreateImplementation_NoDefaultConstructor_ShowsServiceTypeInErrorMessage()
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                ServiceDescription.CreateImplementation<ServiceWithNoDefaultConstructor>());

            // Verify that the error message contains the full type name
            Assert.Contains(typeof(ServiceWithNoDefaultConstructor).FullName, exception.Message);
            Assert.Contains("ServiceWithNoDefaultConstructor", exception.Message);
        }

        [Fact]
        public static void CreateImplementation_WithDefaultConstructor_Succeeds()
        {
            // Act
            var result = ServiceDescription.CreateImplementation<ServiceWithDefaultConstructor>();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ServiceWithDefaultConstructor>(result);
        }
    }

    // Test service without default constructor
    public class ServiceWithNoDefaultConstructor
    {
        private readonly string _dependency;

        public ServiceWithNoDefaultConstructor(string dependency)
        {
            _dependency = dependency;
        }
    }

    // Test service with default constructor
    public class ServiceWithDefaultConstructor
    {
        public ServiceWithDefaultConstructor()
        {
        }
    }
}