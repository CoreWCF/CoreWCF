using System;
using System.Collections;
using System.Reflection;
using System.ServiceModel;
using Xunit.Abstractions;

namespace Helpers
{   
    public class ChannelFactoryCacheCommon
    {
        public const string HelloWorld = "Hello World";
        public const string CurrCacheSetting = "CurrCacheSetting";
        public const string CurrEndpointType = "CurrEndpointType";
        public const string CurrSrvAddress = "CurrSrvAddress";
        public const string CurrEndpointName = "CurrEndpointName";
        public static int PortNumber = -1;

        #region Verification Helpers
        /// <summary>
        /// Get the current count of the internal ChannelFactory cache, factoryRefCache,  in ClientBase of T.  
        /// This factoryRefCache is of type ChannelFactoryRef of T and it is a MruCache.  This MruCache has a private LinkedList called mruList.
        /// </summary>
        public static void VerifyMruListCount<ClientBaseType>(int expectedMruListCount, ITestOutputHelper output)
        {
            int actualMruListCount = GetMruListCount<ClientBaseType>();
            output.WriteLine("[VerifyMruListCount] mruList count is {0}", actualMruListCount);

            if (expectedMruListCount != actualMruListCount)
            {
                throw new Exception(String.Format(
                    "Expected MruListCount={0} but got actual MruListCount={1}",
                    expectedMruListCount,
                    actualMruListCount));
            }
        }

        public static int GetMruListCount<ClientBaseType>()
        {
            // Getting ClientBase<T>.factoryRefCache using reflection
            var factoryRefCacheField = typeof(ClientBaseType).GetField("factoryRefCache", BindingFlags.Static | BindingFlags.NonPublic);
            var ChannelFactoryRef = factoryRefCacheField.GetValue(null);

            // Getting the count of mruList from the ClientBase<T>.factoryRefCache
            Type channelFactoryRefType = ChannelFactoryRef.GetType().BaseType; // getting the base type MruCache<EndpointTrait<T>,ChannelFactoryRef<T>>
            var mruListField = channelFactoryRefType.GetField("mruList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            var mruList = (ICollection)mruListField.GetValue(ChannelFactoryRef);
            return mruList.Count;
        }

        /// <summary>
        /// Determine whether the proxy is currently holding a cached factory
        /// </summary>
        /// <param name="proxy">An instance of ClientBase of T</param>
        public static void VerifyUseCacheFactory<ClientBaseType>(ClientBaseType proxy, bool expectedUseCacheFactory, ITestOutputHelper output)
        {
            // Getting ClientBase<T>.useCachedFactory using reflection
            var useCacheFactoryField = typeof(ClientBaseType).GetField("useCachedFactory", BindingFlags.Instance | BindingFlags.NonPublic);
            var actualUseCacheFactory = (bool)useCacheFactoryField.GetValue(proxy);

            output.WriteLine("[VerifyUseCacheFactory] useCacheFactory is {0}", actualUseCacheFactory);

            if (expectedUseCacheFactory != actualUseCacheFactory)
            {
                throw new Exception(String.Format(
                    "Expected UseCacheFactory={0} but got actual UseCacheFactory={1}",
                    expectedUseCacheFactory,
                    actualUseCacheFactory));
            }
        }

        /// <summary>
        /// Compare the object reference of the given ChannelFactories
        /// </summary>
        public static void CompareChannelFactoryRef(bool isExpectedEqual, ITestOutputHelper output, params ChannelFactory[] factories)
        {
            for (int i = 0; i < factories.Length; i++)
            {
                for (int j = i; j < factories.Length; j++)
                {
                    if (i == j) continue;

                    bool result = isExpectedEqual ? factories[i] == factories[j] : factories[i] != factories[j];

                    if (result)
                    {
                        output.WriteLine("[CompareChannelFactoryRef] The 2 ChannelFactories of the given proxies have {2} object reference(s)",
                            i.ToString(), j.ToString(),
                            isExpectedEqual ? "the SAME" : "DIFFERENT");
                    }
                    else
                    {
                        throw new Exception(String.Format(
                            "The 2 ChannelFactories of the given proxies have {2} object reference(s), but it is incorrect",
                            i.ToString(), j.ToString(),
                            isExpectedEqual ? "the SAME" : "DIFFERENT"));
                    }
                }
            }
        }

        /// <summary>
        /// Simple method to verify the expected and actual strings
        /// </summary>
        public static void VerifyResult(string expected, string actual, ITestOutputHelper output)
        {
            if (expected != actual)
            {
                throw new Exception(string.Format(
                    "Expected={0} but got actual={1}",
                    expected, actual));
            }
            output.WriteLine("Both the expected and actual state is '{0}'", actual);
        }

        /// <summary>
        /// Simple method to verify the expected and actual doubles
        /// </summary>
        public static void VerifyResult(double expected, double actual, ITestOutputHelper output)
        {
            if (expected != actual)
            {
                throw new Exception(string.Format(
                    "Expected={0} but got actual={1}",
                    expected, actual));
            }
            output.WriteLine("Both the expected and actual state is '{0}'", actual);
        }

        public static void VerifyState(CommunicationState actual, CommunicationState expected, ITestOutputHelper output)
        {
            if (expected != actual)
            {
                throw new ApplicationException(String.Format(
                    "The expected state is {0} but the actual state is {1}",
                    expected, actual));
            }
            output.WriteLine("Both the expected and actual state is '{0}'", actual);
        }
        #endregion
    }
}

