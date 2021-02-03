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
        private const bool DefaultHttpTransportPerFactoryConnectionPool = false;
        private const bool DefaultEnsureUniquePerformanceCounterInstanceNames = false;
        private const bool DefaultUseConfiguredTransportSecurityHeaderLayout = false;
        private const bool DefaultUseBestMatchNamedPipeUri = false;
        private const bool DefaultUseLegacyCertificateUsagePolicy = false;
        private const bool DefaultDisableOperationContextAsyncFlow = true;
        private const bool DefaultDeferSslStreamServerCertificateCleanup = false;
        private static bool s_useLegacyCertificateUsagePolicy;
        private static bool s_httpTransportPerFactoryConnectionPool;
        private static bool s_ensureUniquePerformanceCounterInstanceNames;
        private static bool s_useConfiguredTransportSecurityHeaderLayout;
        private static bool s_useBestMatchNamedPipeUri;
        private static bool s_disableOperationContextAsyncFlow;
        private static bool s_deferSslStreamServerCertificateCleanup;
        private static volatile bool s_settingsInitalized = false;
        private static readonly object s_appSettingsLock = new object();

        internal static bool UseLegacyCertificateUsagePolicy
        {
            get
            {
                EnsureSettingsLoaded();

                return s_useLegacyCertificateUsagePolicy;
            }
        }

        internal static bool HttpTransportPerFactoryConnectionPool
        {
            get
            {
                EnsureSettingsLoaded();

                return s_httpTransportPerFactoryConnectionPool;
            }
        }

        internal static bool EnsureUniquePerformanceCounterInstanceNames
        {
            get
            {
                EnsureSettingsLoaded();

                return s_ensureUniquePerformanceCounterInstanceNames;
            }
        }

        internal static bool DisableOperationContextAsyncFlow
        {
            get
            {
                EnsureSettingsLoaded();
                return s_disableOperationContextAsyncFlow;
            }
        }

        internal static bool UseConfiguredTransportSecurityHeaderLayout
        {
            get
            {
                EnsureSettingsLoaded();

                return s_useConfiguredTransportSecurityHeaderLayout;
            }
        }

        internal static bool UseBestMatchNamedPipeUri
        {
            get
            {
                EnsureSettingsLoaded();

                return s_useBestMatchNamedPipeUri;
            }
        }

        internal static bool DeferSslStreamServerCertificateCleanup
        {
            get
            {
                EnsureSettingsLoaded();

                return s_deferSslStreamServerCertificateCleanup;
            }
        }

        private static void EnsureSettingsLoaded()
        {
            if (!s_settingsInitalized)
            {
                lock (s_appSettingsLock)
                {
                    if (!s_settingsInitalized)
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
                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[UseLegacyCertificateUsagePolicyString], out s_useLegacyCertificateUsagePolicy))
                            {
                                s_useLegacyCertificateUsagePolicy = DefaultUseLegacyCertificateUsagePolicy;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[HttpTransportPerFactoryConnectionPoolString], out s_httpTransportPerFactoryConnectionPool))
                            {
                                s_httpTransportPerFactoryConnectionPool = DefaultHttpTransportPerFactoryConnectionPool;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[EnsureUniquePerformanceCounterInstanceNamesString], out s_ensureUniquePerformanceCounterInstanceNames))
                            {
                                s_ensureUniquePerformanceCounterInstanceNames = DefaultEnsureUniquePerformanceCounterInstanceNames;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[DisableOperationContextAsyncFlowString], out s_disableOperationContextAsyncFlow))
                            {
                                s_disableOperationContextAsyncFlow = DefaultDisableOperationContextAsyncFlow;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[UseConfiguredTransportSecurityHeaderLayoutString], out s_useConfiguredTransportSecurityHeaderLayout))
                            {
                                s_useConfiguredTransportSecurityHeaderLayout = DefaultUseConfiguredTransportSecurityHeaderLayout;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[UseBestMatchNamedPipeUriString], out s_useBestMatchNamedPipeUri))
                            {
                                s_useBestMatchNamedPipeUri = DefaultUseBestMatchNamedPipeUri;
                            }

                            if ((appSettingsSection == null) || !bool.TryParse(appSettingsSection[DeferSslStreamServerCertificateCleanupString], out s_deferSslStreamServerCertificateCleanup))
                            {
                                s_deferSslStreamServerCertificateCleanup = DefaultDeferSslStreamServerCertificateCleanup;
                            }

                            s_settingsInitalized = true;
                        }
                    }
                }
            }
        }
    }
}
