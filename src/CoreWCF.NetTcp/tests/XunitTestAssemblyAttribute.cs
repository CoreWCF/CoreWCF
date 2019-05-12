using Xunit;

// Registry tests can conflict with each other due to accessing the same keys/values in the registry
[assembly: CollectionBehavior(DisableTestParallelization = true)]
