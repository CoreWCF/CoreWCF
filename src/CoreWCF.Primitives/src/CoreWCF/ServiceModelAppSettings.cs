// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.Configuration;

namespace CoreWCF
{
    // Due to friend relationships with other assemblies, naming this class as AppSettings causes ambiguity when building those assemblies
    internal static class ServiceModelAppSettings
    {
        internal const string HttpTransportPerFactoryConnectionPoolString = "wcf:httpTransportBinding:useUniqueConnectionPoolPerFactory";
        internal const string EnsureUniquePerformanceCounterInstanceNamesString = "wcf:ensureUniquePerformanceCounterInstanceNames";
        internal const string UseConfiguredTransportSecurityHeaderLayoutString = "wcf:useConfiguredTransportSecurityHeaderLayout";
        internal const string UseBestMatchNamedPipeUriString = "wcf:useBestMatchNamedPipeUri";
        internal const string DisableOperationContextAsyncFlowString = "wcf:disableOperationContextAsyncFlow";
        internal const string UseLegacyCertificateUsagePolicyString = "wcf:useLegacyCertificateUsagePolicy";
        internal const string DeferSslStreamServerCertificateCleanupString = "wcf:deferSslStreamServerCertificateCleanup";

        const bool DefaultHttpTransportPerFactoryConnectionPool = false;
        const bool DefaultEnsureUniquePerformanceCounterInstanceNames = false;
        const bool DefaultUseConfiguredTransportSecurityHeaderLayout = false;
        const bool DefaultUseBestMatchNamedPipeUri = false;
        const bool DefaultUseLegacyCertificateUsagePolicy = false;
        const bool DefaultDisableOperationContextAsyncFlow = true;
        const bool DefaultDeferSslStreamServerCertificateCleanup = false;

        static bool useLegacyCertificateUsagePolicy;
        static bool httpTransportPerFactoryConnectionPool;
        static bool ensureUniquePerformanceCounterInstanceNames;
        static bool useConfiguredTransportSecurityHeaderLayout;
        static bool useBestMatchNamedPipeUri;
        static bool disableOperationContextAsyncFlow;
        static bool deferSslStreamServerCertificateCleanup;
        static volatile bool settingsInitalized = false;
        static object appSettingsLock = new object();

        internal static bool UseLegacyCertificateUsagePolicy
        {
            get
            {
                EnsureSettingsLoaded();

                return useLegacyCertificateUsagePolicy;
            }
        }

        internal static bool HttpTransportPerFactoryConnectionPool
        {
            get
            {
                EnsureSettingsLoaded();

                return httpTransportPerFactoryConnectionPool;
            }
        }

        internal static bool EnsureUniquePerformanceCounterInstanceNames
        {
            get
            {
                EnsureSettingsLoaded();

                return ensureUniquePerformanceCounterInstanceNames;
            }
        }

        internal static bool DisableOperationContextAsyncFlow
        {
            get
            {
                EnsureSettingsLoaded();
                return disableOperationContextAsyncFlow;
            }
        }

        internal static bool UseConfiguredTransportSecurityHeaderLayout
        {
            get
            {
                EnsureSettingsLoaded();

                return useConfiguredTransportSecurityHeaderLayout;
            }
        }

        internal static bool UseBestMatchNamedPipeUri
        {
            get
            {
                EnsureSettingsLoaded();

                return useBestMatchNamedPipeUri;
            }
        }

        internal static bool DeferSslStreamServerCertificateCleanup
        {
            get
            {
                EnsureSettingsLoaded();

                return deferSslStreamServerCertificateCleanup;
            }
        }

        static void EnsureSettingsLoaded()
        {
            if (!settingsInitalized)
            {
                lock (appSettingsLock)
                {
                    if (!settingsInitalized)
                    {
                        NameValueCollection appSettingsSection = null;
                        try
                        {
                            appSettingsSection = ConfigurationManager.AppSettings;
                        }
                        catch (ConfigurationErrorsException)
                        {
                        }
                        finally
                        {
                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[UseLegacyCertificateUsagePolicyString], out useLegacyCertificateUsagePolicy))
                            {
                                useLegacyCertificateUsagePolicy = DefaultUseLegacyCertificateUsagePolicy;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[HttpTransportPerFactoryConnectionPoolString], out httpTransportPerFactoryConnectionPool))
                            {
                                httpTransportPerFactoryConnectionPool = DefaultHttpTransportPerFactoryConnectionPool;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[EnsureUniquePerformanceCounterInstanceNamesString], out ensureUniquePerformanceCounterInstanceNames))
                            {
                                ensureUniquePerformanceCounterInstanceNames = DefaultEnsureUniquePerformanceCounterInstanceNames;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[DisableOperationContextAsyncFlowString], out disableOperationContextAsyncFlow))
                            {
                                disableOperationContextAsyncFlow = DefaultDisableOperationContextAsyncFlow;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[UseConfiguredTransportSecurityHeaderLayoutString], out useConfiguredTransportSecurityHeaderLayout))
                            {
                                useConfiguredTransportSecurityHeaderLayout = DefaultUseConfiguredTransportSecurityHeaderLayout;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[UseBestMatchNamedPipeUriString], out useBestMatchNamedPipeUri))
                            {
                                useBestMatchNamedPipeUri = DefaultUseBestMatchNamedPipeUri;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[DeferSslStreamServerCertificateCleanupString], out deferSslStreamServerCertificateCleanup))
                            {
                                deferSslStreamServerCertificateCleanup = DefaultDeferSslStreamServerCertificateCleanup;
                            }

                            settingsInitalized = true;
                        }
                    }
                }
            }
        }
    }
}
