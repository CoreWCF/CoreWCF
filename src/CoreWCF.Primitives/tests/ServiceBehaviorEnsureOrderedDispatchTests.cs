// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public static class ServiceBehaviorEnsureOrderedDispatchTests
    {
        [Fact]
        public static void DefaultValueTest()
        {
            ServiceBehaviorAttribute attribute = new ServiceBehaviorAttribute();
            Assert.False(attribute.EnsureOrderedDispatch);
        }

        [Fact]
        public static void SetAndGetValueTest()
        {
            ServiceBehaviorAttribute attribute = new ServiceBehaviorAttribute();
            
            // Test setting to true
            attribute.EnsureOrderedDispatch = true;
            Assert.True(attribute.EnsureOrderedDispatch);
            
            // Test setting to false
            attribute.EnsureOrderedDispatch = false;
            Assert.False(attribute.EnsureOrderedDispatch);
        }

        [Fact]
        public static void EnsureOrderedDispatch_WithConcurrencyModeSingle_Succeeds()
        {
            // Arrange
            ServiceBehaviorAttribute attribute = new ServiceBehaviorAttribute
            {
                ConcurrencyMode = ConcurrencyMode.Single,
                EnsureOrderedDispatch = true
            };
            
            var serviceHost = new TestServiceHost();
            var serviceDescription = new ServiceDescription
            {
                Name = "TestService"
            };
            
            // Act & Assert - should not throw
            IServiceBehavior behavior = attribute;
            behavior.Validate(serviceDescription, serviceHost);
        }

        [Fact]
        public static void EnsureOrderedDispatch_WithConcurrencyModeReentrant_ThrowsInvalidOperationException()
        {
            // Arrange
            ServiceBehaviorAttribute attribute = new ServiceBehaviorAttribute
            {
                ConcurrencyMode = ConcurrencyMode.Reentrant,
                EnsureOrderedDispatch = true
            };
            
            var serviceHost = new TestServiceHost();
            var serviceDescription = new ServiceDescription
            {
                Name = "TestService"
            };
            
            // Act & Assert
            IServiceBehavior behavior = attribute;
            Assert.Throws<InvalidOperationException>(() => behavior.Validate(serviceDescription, serviceHost));
        }

        [Fact]
        public static void EnsureOrderedDispatch_WithConcurrencyModeMultiple_ThrowsInvalidOperationException()
        {
            // Arrange
            ServiceBehaviorAttribute attribute = new ServiceBehaviorAttribute
            {
                ConcurrencyMode = ConcurrencyMode.Multiple,
                EnsureOrderedDispatch = true
            };
            
            var serviceHost = new TestServiceHost();
            var serviceDescription = new ServiceDescription
            {
                Name = "TestService"
            };
            
            // Act & Assert
            IServiceBehavior behavior = attribute;
            Assert.Throws<InvalidOperationException>(() => behavior.Validate(serviceDescription, serviceHost));
        }

        [Fact]
        public static void EnsureOrderedDispatch_False_WithAnyModeSucceeds()
        {
            var serviceHost = new TestServiceHost();
            var serviceDescription = new ServiceDescription
            {
                Name = "TestService"
            };
            
            // Test with Reentrant
            ServiceBehaviorAttribute attribute1 = new ServiceBehaviorAttribute
            {
                ConcurrencyMode = ConcurrencyMode.Reentrant,
                EnsureOrderedDispatch = false
            };
            
            IServiceBehavior behavior1 = attribute1;
            behavior1.Validate(serviceDescription, serviceHost);
            
            // Test with Multiple
            ServiceBehaviorAttribute attribute2 = new ServiceBehaviorAttribute
            {
                ConcurrencyMode = ConcurrencyMode.Multiple,
                EnsureOrderedDispatch = false
            };
            
            IServiceBehavior behavior2 = attribute2;
            behavior2.Validate(serviceDescription, serviceHost);
        }

        // Simple test service host for testing
        private class TestServiceHost : ServiceHostBase
        {
            protected override ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts)
            {
                implementedContracts = new Dictionary<string, ContractDescription>();
                return new ServiceDescription();
            }

            protected override void OnAbort()
            {
                // No-op for testing
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            protected override Task OnOpenAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }
        }
    }
}
