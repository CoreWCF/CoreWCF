﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CoreWCF.Channels
{
    internal static class DnsCache
    {
        private const string LinuxProcHostnamePath = "/proc/sys/kernel/hostname";

        // Double-checked locking pattern requires volatile for read/write synchronization
        private static volatile string machineName;

        private static object ThisLock { get; } = new object();

        public static string MachineName
        {
            get
            {
                if (machineName == null)
                {
                    lock (ThisLock)
                    {
                        if (machineName == null)
                        {
                            try
                            {
                                machineName = Dns.GetHostEntry(String.Empty).HostName;
                            }
                            catch (SocketException exception)
                            {
                                DiagnosticUtility.TraceHandledException(exception,
                                        TraceEventType.Information);
                            }
                        }

                        if (machineName == null) // prior attempt failed
                        {
                            try
                            {
                                // This uses a different native API so might work where the previous one didn't.
                                machineName = Dns.GetHostName();
                            }
                            catch (SocketException exception)
                            {
                                DiagnosticUtility.TraceHandledException(exception,
                                        TraceEventType.Information);
                            }
                        }

                        if (machineName == null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists(LinuxProcHostnamePath))
                        {
                            try
                            {
                                // Final attempt, try to get the hostname from the proc filesystem if running on Linux
                                string[] hostnameFile = File.ReadAllLines(LinuxProcHostnamePath);
                                if (hostnameFile.Length > 0 && !string.IsNullOrEmpty(hostnameFile[0]))
                                {
                                    machineName = hostnameFile[0];
                                }
                            }
                            catch (Exception exception) // There's no common base exception that File.ReadAllLines can throw
                            {
                                DiagnosticUtility.TraceHandledException(exception,
                                    TraceEventType.Information);
                            }
                        }

                        // Final fallback if every other mechanism fails
                        if (machineName == null)
                        {
                            machineName = "localhost";
                        }
                    }
                }

                return machineName;
            }
        }
    }
}