// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace CoreWCF.UnixDomainSocket.Tests
{
    public class PosixIdentityResolutionTests
    {
        private const string InteropTypeName = "CoreWCF.UnixDomainSocket.Security.NativeSysCall";

        private static readonly Type s_interopType = typeof(CoreWCF.UnixDomainSocketBinding).Assembly
            .GetType(InteropTypeName, throwOnError: true);
        private static readonly MethodInfo s_getUserInfo = s_interopType.GetMethod(
            "GetUserInfo", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo s_getGroupInfo = s_interopType.GetMethod(
            "GetGroupInfo", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        [LinuxOnlyFact]
        public void GetUserInfo_ConcurrentLookups_ReturnMatchingUid()
        {
            (uint uidA, string nameA, uint uidB, string nameB) = FindTwoResolvableUsers();
            RunConcurrentLookups(
                resolver: uid =>
                {
                    object info = s_getUserInfo.Invoke(null, new object[] { uid });
                    if (info == null) return (false, default, null);
                    uint actualUid = (uint)info.GetType().GetProperty("Uid").GetValue(info);
                    string actualName = (string)info.GetType().GetProperty("Name").GetValue(info);
                    return (true, actualUid, actualName);
                },
                idA: uidA, nameA: nameA, idB: uidB, nameB: nameB,
                idLabel: "uid");
        }

        [LinuxOnlyFact]
        public void GetGroupInfo_ConcurrentLookups_ReturnMatchingGid()
        {
            (uint gidA, string nameA, uint gidB, string nameB) = FindTwoResolvableGroups();
            RunConcurrentLookups(
                resolver: gid =>
                {
                    object info = s_getGroupInfo.Invoke(null, new object[] { gid });
                    if (info == null) return (false, default, null);
                    uint actualGid = (uint)info.GetType().GetProperty("Id").GetValue(info);
                    string actualName = (string)info.GetType().GetProperty("Name").GetValue(info);
                    return (true, actualGid, actualName);
                },
                idA: gidA, nameA: nameA, idB: gidB, nameB: nameB,
                idLabel: "gid");
        }

        private static void RunConcurrentLookups(
            Func<uint, (bool found, uint id, string name)> resolver,
            uint idA, string nameA, uint idB, string nameB, string idLabel)
        {
            const int threadCount = 16;
            const int durationMs = 4000;

            var stop = new ManualResetEventSlim(false);
            var start = new ManualResetEventSlim(false);
            var mismatches = new List<string>();
            var mismatchLock = new object();
            long iterations = 0;
            Exception workerException = null;

            var threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                bool useA = (i % 2) == 0;
                uint expectedId = useA ? idA : idB;
                string expectedName = useA ? nameA : nameB;

                threads[i] = new Thread(() =>
                {
                    try
                    {
                        start.Wait();
                        while (!stop.IsSet)
                        {
                            (bool found, uint actualId, string actualName) = resolver(expectedId);
                            Interlocked.Increment(ref iterations);
                            if (!found)
                            {
                                continue;
                            }
                            if (actualId != expectedId || !string.Equals(actualName, expectedName, StringComparison.Ordinal))
                            {
                                lock (mismatchLock)
                                {
                                    if (mismatches.Count < 5)
                                    {
                                        mismatches.Add(
                                            $"requested {idLabel}={expectedId} ('{expectedName}') but received {idLabel}={actualId} name='{actualName}'");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref workerException, ex, null);
                    }
                })
                { IsBackground = true, Name = $"PosixLookup-{idLabel}-{i}" };
                threads[i].Start();
            }

            start.Set();
            Thread.Sleep(durationMs);
            stop.Set();
            foreach (Thread t in threads)
            {
                t.Join();
            }

            Assert.Null(workerException);
            Assert.True(iterations > 0, "no iterations completed");
            Assert.True(
                mismatches.Count == 0,
                $"Resolver returned mismatched results in {mismatches.Count} of {iterations} concurrent lookups; sample: {string.Join(" | ", mismatches)}");
        }

        private static (uint idA, string nameA, uint idB, string nameB) FindTwoResolvableUsers()
            => FindTwoResolvable(
                getEffective: GetEffectiveUid,
                resolve: id =>
                {
                    object info = s_getUserInfo.Invoke(null, new object[] { id });
                    if (info == null) return (false, default, null);
                    uint actualId = (uint)info.GetType().GetProperty("Uid").GetValue(info);
                    string name = (string)info.GetType().GetProperty("Name").GetValue(info);
                    return (true, actualId, name);
                },
                kind: "user");

        private static (uint idA, string nameA, uint idB, string nameB) FindTwoResolvableGroups()
            => FindTwoResolvable(
                getEffective: GetEffectiveGid,
                resolve: id =>
                {
                    object info = s_getGroupInfo.Invoke(null, new object[] { id });
                    if (info == null) return (false, default, null);
                    uint actualId = (uint)info.GetType().GetProperty("Id").GetValue(info);
                    string name = (string)info.GetType().GetProperty("Name").GetValue(info);
                    return (true, actualId, name);
                },
                kind: "group");

        private static (uint idA, string nameA, uint idB, string nameB) FindTwoResolvable(
            Func<uint> getEffective,
            Func<uint, (bool found, uint id, string name)> resolve,
            string kind)
        {
            var found = new List<(uint Id, string Name)>();
            var seen = new HashSet<uint>();
            var candidates = new List<uint> { 0, getEffective() };
            // Probe a small range to handle environments where the calling identity is
            // root (uid 0) — in which case we still need a second distinct entry.
            for (uint i = 1; i <= 200; i++)
            {
                candidates.Add(i);
            }

            foreach (uint id in candidates)
            {
                if (!seen.Add(id))
                {
                    continue;
                }
                (bool ok, uint actualId, string name) = resolve(id);
                if (!ok || actualId != id || string.IsNullOrEmpty(name))
                {
                    continue;
                }
                found.Add((id, name));
                if (found.Count == 2)
                {
                    return (found[0].Id, found[0].Name, found[1].Id, found[1].Name);
                }
            }

            throw new InvalidOperationException(
                $"Test prerequisite not met: could not find two distinct resolvable {kind} entries.");
        }

        [DllImport("libc", EntryPoint = "geteuid")]
        private static extern uint GetEffectiveUid();

        [DllImport("libc", EntryPoint = "getegid")]
        private static extern uint GetEffectiveGid();
    }
}
