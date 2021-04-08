// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace CoreWCF.Security
{
    public class LdapSettings
    {
        private LdapConnection _ldapConnection;
        /// <summary>
        /// Default constructor. 
        /// </summary>
        /// <param name="servers">List of ldap servers</param>
        /// <param name="domain">domain name to search for</param>
        /// <param name="orgUnit"> org unit to search for. This is required per the issue https://github.com/dotnet/runtime/issues/44826 for CoreWCF to work cross platform, once upgraded to .NET6, this is not required anymore. </param>
        public LdapSettings(string server, string domain, string orgUnit) : this(new string[] { server }, domain, orgUnit)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="domain"></param>
        /// <param name="machineAccountName"></param>
        /// <param name="machineAccountPassword"></param>
        /// <param name="orgUnit"></param>
        public LdapSettings(string server, string domain, string machineAccountName, string machineAccountPassword, string orgUnit)
            : this(new string[] { server }, domain, machineAccountName, machineAccountPassword, orgUnit)
        {

        }
        /// <summary>
        /// Default constructor. 
        /// </summary>
        /// <param name="servers">List of ldap servers</param>
        /// <param name="domain">domain name to search for</param>
        /// <param name="orgUnit"> This is required </param>
        public LdapSettings(string[] servers, string domain, string orgUnit) : this(servers, domain, null, null, orgUnit)
        {

        }

        /// <summary>
        /// Constructor to pass machine account name and machine account password.
        /// </summary>
        /// <param name="servers"></param>
        /// <param name="domain"></param>
        /// <param name="machineAccountName"></param>
        /// <param name="machineAccountPassword"></param>
        public LdapSettings(string[] servers, string domain, string machineAccountName, string machineAccountPassword, string orgUnit)
        {
            Servers = servers;
            Domain = domain;
            MachineAccountName = machineAccountName;
            MachineAccountPassword = machineAccountPassword;
            OrgUnit = orgUnit;
            Validate();
            EnableLdapClaimResolution = true;
        }

        /// <summary>
        /// list of servers for ldap search
        /// </summary>
        public string[] Servers { get; }
        /// <summary>
        /// Configure whether LDAP connection should be used to resolve claims.
        /// This is mainly used on Linux.
        /// </summary>
        [DefaultValue(true)]
        public bool EnableLdapClaimResolution { get; set; }

        /// <summary>
        /// The domain to use for the LDAP connection. This is a mandatory setting.
        /// </summary>
        /// <example>
        /// DOMAIN.com
        /// </example>
        public string Domain { get; }

        /// <summary>
        /// Organization unit to start with. If not provided, it will search from top domain.
        /// </summary>
        public string OrgUnit { get; }

        /// <summary>
        /// The machine account name to use when opening the LDAP connection.
        /// If this is not provided, the machine wide credentials of the
        /// domain joined machine will be used.
        /// </summary>
        public string MachineAccountName { get; }

        /// <summary>
        /// The machine account password to use when opening the LDAP connection.
        /// This must be provided if a <see cref="MachineAccountName"/> is provided.
        /// </summary>
        public string MachineAccountPassword { get; }

        /// <summary>
        /// This option indicates whether nested groups should be ignored when
        /// resolving Roles. The default is false.
        /// </summary>
        public bool IgnoreNestedGroups { get; set; }

        /// <summary>
        /// The <see cref="LdapConnection"/> to be used to retrieve role claims.
        /// If no explicit connection is provided, an LDAP connection will be
        /// automatically created based on the <see cref="Domain"/>,
        /// <see cref="MachineAccountName"/> and <see cref="MachineAccountPassword"/>
        /// options. If provided, this connection will be used and the
        /// <see cref="Domain"/>, <see cref="MachineAccountName"/> and
        /// <see cref="MachineAccountPassword"/>  options will not be used to create
        /// the <see cref="LdapConnection"/>.
        /// </summary>
        internal LdapConnection LdapConnection
        {
            get
            {
                if (_ldapConnection == null)
                    _ldapConnection = ConnectLDAP();
                return _ldapConnection;
            }
        }

        /// <summary>
        /// The sliding expiration that should be used for entries in the cache for user claims, defaults to 10 minutes.
        /// This is a sliding expiration that will extend each time claims for a user is retrieved.
        /// </summary>
        public TimeSpan ClaimsCacheSlidingExpiration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The absolute expiration that should be used for entries in the cache for user claims, defaults to 60 minutes.
        /// This is an absolute expiration that starts when a claims for a user is retrieved for the first time.
        /// </summary>
        public TimeSpan ClaimsCacheAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(60);

        /// <summary>
        /// The maximum size of the claim results cache, defaults to 100 MB.
        /// </summary>
        public int ClaimsCacheSize { get; set; } = 100 * 1024 * 1024;

        internal MemoryCache ClaimsCache { get; set; }

        /// <summary>
        /// Validates the <see cref="LdapSettings"/>.
        /// </summary>
        public void Validate()
        {
            if (EnableLdapClaimResolution)
            {
                if (Servers == null || Servers.Length == 0)
                {
                    throw new ArgumentException($"{nameof(EnableLdapClaimResolution)} is set to true but ldap server not set.");
                }
                if (string.IsNullOrEmpty(Domain))
                {
                    throw new ArgumentException($"{nameof(EnableLdapClaimResolution)} is set to true but {nameof(Domain)} is not set.");
                }

                if (string.IsNullOrEmpty(MachineAccountName) && !string.IsNullOrEmpty(MachineAccountPassword))
                {
                    throw new ArgumentException($"{nameof(MachineAccountPassword)} should only be specified when {nameof(MachineAccountName)} is configured.");
                }
            }
        }

        private LdapConnection ConnectLDAP()
        {
            if (EnableLdapClaimResolution)
            {
                Validate();
                var di = new LdapDirectoryIdentifier(Servers, true, false);
                if (string.IsNullOrEmpty(MachineAccountName))
                {
                    // Use default credentials
                    _ldapConnection = new LdapConnection(di, null, AuthType.Kerberos);
                }
                else
                {
                    // Use specific specific machine account
                    var machineAccount = MachineAccountName + "@" + Domain;
                    var credentials = new NetworkCredential(machineAccount, MachineAccountPassword);
                    _ldapConnection = new LdapConnection(di, credentials);
                }
                _ldapConnection.SessionOptions.ProtocolVersion = 3; //Setting LDAP Protocol to latest version
                _ldapConnection.Timeout = TimeSpan.FromMinutes(1);

                _ldapConnection.Bind(); // This line actually makes the connection.
            }
            return _ldapConnection;
        }
    }
}
