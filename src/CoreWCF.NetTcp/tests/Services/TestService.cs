// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Claims;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using ServiceContract;

namespace Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class TestService : ServiceContract.ITestService
    {
        private ManualResetEvent _mre = new ManualResetEvent(false);

        public string EchoString(string echo)
        {
            return echo;
        }

        public bool WaitForSecondRequest()
        {
            _mre.Reset();
            return _mre.WaitOne(TimeSpan.FromSeconds(10));
        }

        public void SecondRequest()
        {
            _mre.Set();
        }

        public string GetClientIpEndpoint()
        {
            if (OperationContext.Current.IncomingMessageProperties.TryGetValue(RemoteEndpointMessageProperty.Name, out object remoteEndpointObj))
            {
                var remoteEndpoint = remoteEndpointObj as RemoteEndpointMessageProperty;
                return remoteEndpoint.Address.ToString() + ":" + remoteEndpoint.Port.ToString();
            }

            throw new Exception("Remote endpoint message property not found");
        }

        public TestMessage TestMessageContract(TestMessage testMessage)
        {
            string text = new StreamReader(testMessage.Body, Encoding.UTF8).ReadToEnd();
            return new TestMessage()
            {
                Header = testMessage.Header + " from server",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(text + " from server"))
            };
        }


        public string EchoForPermission(string echo)
        {
            IPrincipal principal = Thread.CurrentPrincipal;
            if (echo.Contains(PrincipalPermissionMode.UseWindowsGroups.ToString()))
            {
                if (typeof(WindowsIdentity).IsAssignableFrom(principal.Identity?.GetType()))
                    return echo;
                else return "false";

            }
            else if (echo.Contains(PrincipalPermissionMode.Always.ToString()))
            {
                if (principal.Identity != null
                    && principal.Identity.GetType().Equals(typeof(ClaimsIdentity)))
                    return echo;
                else return "false";

            }
            return echo;
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public string EchoForImpersonation(String value)
        {
            return value;
        }
    }
}