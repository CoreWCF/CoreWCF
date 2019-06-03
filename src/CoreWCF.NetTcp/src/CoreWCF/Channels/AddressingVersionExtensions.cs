using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CoreWCF.Channels
{
    internal static class AddressingVersionExtensions
    {
        static AddressingVersionExtensions()
        {
            s_namespaceGetter = GetStringGetterForProperty("Namespace");
            s_faultActionGetter = GetStringGetterForProperty("FaultAction");
            s_anonymousUriGetter = GetUriGetterForProperty("AnonymousUri");
            s_noneUriGetter = GetUriGetterForProperty("NoneUri");
        }

        private static StringGetter GetStringGetterForProperty(string propName)
        {
            // NonPublic and public in case they are made public in the future. Currently they are internal
            var addrVerType = typeof(AddressingVersion);
            var getterPropInfo = typeof(AddressingVersion).GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var getterMethodInfo = getterPropInfo.GetGetMethod(true);
            return (StringGetter)getterMethodInfo.CreateDelegate(typeof(StringGetter));
        }

        private delegate string StringGetter(AddressingVersion addressingVersion);

        private static StringGetter s_namespaceGetter;
        private static StringGetter s_faultActionGetter;

        public static string Namespace(this AddressingVersion addressingVersion)
        {
            return s_namespaceGetter(addressingVersion);
        }

        public static string FaultAction(this AddressingVersion addressingVersion)
        {
            return s_faultActionGetter(addressingVersion);
        }

        private static UriGetter GetUriGetterForProperty(string propName)
        {
            // NonPublic and public in case they are made public in the future. Currently they are internal
            var getterPropInfo = typeof(AddressingVersion).GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var getterMethodInfo = getterPropInfo.GetGetMethod(true);
            return (UriGetter)getterMethodInfo.CreateDelegate(typeof(UriGetter));
        }

        private delegate Uri UriGetter(AddressingVersion addressingVersion);

        private static UriGetter s_anonymousUriGetter;
        private static UriGetter s_noneUriGetter;

        public static Uri AnonymousUri(this AddressingVersion addressingVersion)
        {
            return s_anonymousUriGetter(addressingVersion);
        }

        public static Uri NoneUri(this AddressingVersion addressingVersion)
        {
            return s_noneUriGetter(addressingVersion);
        }
    }
}
