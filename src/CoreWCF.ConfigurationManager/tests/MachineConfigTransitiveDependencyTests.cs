using System;
using System.IO;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class MachineConfigTransitiveDependencyTests
    {
        [Fact]
        public void MachineConfig_ShouldExist_InOutputDirectory()
        {
            // Test that CoreWCF.machine.config is copied to the output directory
            // This test validates the fix for issue #1619
            
            var configPath = Path.Combine(AppContext.BaseDirectory, "CoreWCF.machine.config");
            
            Assert.True(File.Exists(configPath), 
                $"CoreWCF.machine.config should exist at {configPath}. " +
                "This file is required for CoreWCF configuration and should be " +
                "copied to the output directory even when CoreWCF.ConfigurationManager " +
                "is referenced as a transitive dependency.");
        }
        
        [Fact]
        public void MachineConfig_ShouldContain_ServiceModelConfiguration()
        {
            // Test that the machine config contains the expected content
            var configPath = Path.Combine(AppContext.BaseDirectory, "CoreWCF.machine.config");
            
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                
                Assert.Contains("system.serviceModel", content);
                Assert.Contains("CoreWCF.Configuration.ServiceModelSectionGroup", content);
                Assert.Contains("CoreWCF.ConfigurationManager", content);
            }
            else
            {
                // If the file doesn't exist, fail with a helpful message
                Assert.True(false, 
                    $"CoreWCF.machine.config not found at {configPath}. " +
                    "Cannot validate content. This indicates the transitive dependency issue is not fixed.");
            }
        }
    }
}