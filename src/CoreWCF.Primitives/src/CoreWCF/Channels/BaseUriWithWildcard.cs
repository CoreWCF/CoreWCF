// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    // TODO: Make internal again
    [DataContract]
    public sealed class BaseUriWithWildcard
    {
        [DataMember]
        private readonly Uri _baseAddress;
        private const char segmentDelimiter = '/';

        [DataMember]
        private HostNameComparisonMode _hostNameComparisonMode;
        private const string plus = "+";
        private const string star = "*";
        private const int HttpUriDefaultPort = 80;
        private const int HttpsUriDefaultPort = 443;

        // Derived from [DataMember] fields
        private Comparand _comparand;
        private int _hashCode;

        public BaseUriWithWildcard(Uri baseAddress, HostNameComparisonMode hostNameComparisonMode)
        {
            _baseAddress = baseAddress;
            _hostNameComparisonMode = hostNameComparisonMode;
            SetComparisonAddressAndHashCode();

            // Note the Uri may contain query string for WSDL purpose.
            // So do not check IsValid().
        }

        private BaseUriWithWildcard(string protocol, int defaultPort, string binding, int segmentCount, string path, string sampleBinding)
        {
            string[] urlParameters = SplitBinding(binding);

            if (urlParameters.Length != segmentCount)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new UriFormatException(SR.Format(SR.Hosting_MisformattedBinding, binding, protocol, sampleBinding)));
            }

            int currentIndex = segmentCount - 1;
            string host = ParseHostAndHostNameComparisonMode(urlParameters[currentIndex]);

            int port = -1;

            if (--currentIndex >= 0)
            {
                string portString = urlParameters[currentIndex].Trim();

                if (!string.IsNullOrEmpty(portString) &&
                    !int.TryParse(portString, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out port))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new UriFormatException(SR.Format(SR.Hosting_MisformattedPort, protocol, binding, portString)));
                }

                if (port == defaultPort)
                {
                    // Set to -1 so that Uri does not show it in the string.
                    port = -1;
                }
            }
            try
            {
                Fx.Assert(path != null, "path should never be null here");
                _baseAddress = new UriBuilder(protocol, host, port, path).Uri;
            }
            catch (Exception exception)
            {
                if (Fx.IsFatal(exception))
                {
                    throw;
                }

                DiagnosticUtility.TraceHandledException(exception, TraceEventType.Error);

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new UriFormatException(SR.Format(SR.Hosting_MisformattedBindingData, binding,
                    protocol)));
            }
            SetComparisonAddressAndHashCode();
        }

        // TODO: Make internal again
        public Uri BaseAddress
        {
            get { return _baseAddress; }
        }

        // TODO: Make internal again
        public HostNameComparisonMode HostNameComparisonMode
        {
            get { return _hostNameComparisonMode; }
        }

        private static string[] SplitBinding(string binding)
        {
            bool parsingIPv6Address = false;
            string[] tokens = null;
            const char splitChar = ':', startIPv6Address = '[', endIPv6Address = ']';

            List<int> splitLocations = null;

            for (int i = 0; i < binding.Length; i++)
            {
                if (parsingIPv6Address && binding[i] == endIPv6Address)
                {
                    parsingIPv6Address = false;
                }
                else if (binding[i] == startIPv6Address)
                {
                    parsingIPv6Address = true;
                }
                else if (!parsingIPv6Address && binding[i] == splitChar)
                {
                    if (splitLocations == null)
                    {
                        splitLocations = new List<int>();
                    }
                    splitLocations.Add(i);
                }
            }

            if (splitLocations == null)
            {
                tokens = new[] { binding };
            }
            else
            {
                tokens = new string[splitLocations.Count + 1];
                int startIndex = 0;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (i < splitLocations.Count)
                    {
                        int nextSplitIndex = splitLocations[i];
                        tokens[i] = binding.Substring(startIndex, nextSplitIndex - startIndex);
                        startIndex = nextSplitIndex + 1;
                    }
                    else //splitting the last segment
                    {
                        if (startIndex < binding.Length)
                        {
                            tokens[i] = binding.Substring(startIndex, binding.Length - startIndex);
                        }
                        else
                        {
                            //splitChar was the last character in the string
                            tokens[i] = string.Empty;
                        }
                    }
                }
            }
            return tokens;
        }

        internal static BaseUriWithWildcard CreateHostedUri(string protocol, string binding, string path)
        {
            Fx.Assert(protocol != null, "caller must verify");

            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }

            if (path == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(path));
            }

            if (protocol.Equals(UriEx.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                // For http, binding format is: "<ipAddress>:<port>:<hostName>"
                // as specified in http://www.microsoft.com/resources/documentation/WindowsServ/2003/standard/proddocs/en-us/Default.asp?url=/resources/documentation/WindowsServ/2003/standard/proddocs/en-us/ref_mb_serverbindings.asp
                return new BaseUriWithWildcard(UriEx.UriSchemeHttp, HttpUriDefaultPort, binding, 3, path, ":80:");
            }
            if (protocol.Equals(UriEx.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                // For https, binding format is the same as http
                return new BaseUriWithWildcard(UriEx.UriSchemeHttps, HttpsUriDefaultPort, binding, 3, path, ":443:");
            }
            if (protocol.Equals(UriEx.UriSchemeNetTcp, StringComparison.OrdinalIgnoreCase))
            {
                // For net.tcp, binding format is: "<port>:<hostName>"
                return new BaseUriWithWildcard(UriEx.UriSchemeNetTcp, TransportDefaults.TcpUriDefaultPort, binding, 2, path, "808:*");
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new UriFormatException(SR.Format(SR.Hosting_NotSupportedProtocol, binding)));
        }

        public override bool Equals(object o)
        {
            BaseUriWithWildcard other = o as BaseUriWithWildcard;

            if (other == null || other._hashCode != _hashCode || other._hostNameComparisonMode != _hostNameComparisonMode ||
                other._comparand.Port != _comparand.Port)
            {
                return false;
            }
            if (!ReferenceEquals(other._comparand.Scheme, _comparand.Scheme))
            {
                return false;
            }
            return _comparand.Address.Equals(other._comparand.Address);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        internal bool IsBaseOf(Uri fullAddress)
        {
            if (_baseAddress.Scheme != (object)fullAddress.Scheme)
            {
                return false;
            }

            if (_baseAddress.Port != fullAddress.Port)
            {
                return false;
            }

            if (HostNameComparisonMode == HostNameComparisonMode.Exact)
            {
                if (string.Compare(_baseAddress.Host, fullAddress.Host, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }
            string s1 = _baseAddress.GetComponents(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.Unescaped);
            string s2 = fullAddress.GetComponents(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.Unescaped);

            if (s1.Length > s2.Length)
            {
                return false;
            }

            if (s1.Length < s2.Length &&
                s1[s1.Length - 1] != segmentDelimiter &&
                s2[s1.Length] != segmentDelimiter)
            {
                // Matching over segments
                return false;
            }
            return string.Compare(s2, 0, s1, 0, s1.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            UriSchemeKeyedCollection.ValidateBaseAddress(_baseAddress, "context");

            if (!HostNameComparisonModeHelper.IsDefined(HostNameComparisonMode))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("context", SR.Hosting_BaseUriDeserializedNotValid);
            }
            SetComparisonAddressAndHashCode();
        }

        private string ParseHostAndHostNameComparisonMode(string host)
        {
            if (string.IsNullOrEmpty(host) || host.Equals(star))
            {
                _hostNameComparisonMode = HostNameComparisonMode.WeakWildcard;
                host = DnsCache.MachineName;
            }
            else if (host.Equals(plus))
            {
                _hostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
                host = DnsCache.MachineName;
            }
            else
            {
                _hostNameComparisonMode = HostNameComparisonMode.Exact;
            }
            return host;
        }

        private void SetComparisonAddressAndHashCode()
        {
            if (HostNameComparisonMode == HostNameComparisonMode.Exact)
            {
                // Use canonical string representation of the full base address for comparison
                _comparand.Address = _baseAddress.ToString();
            }
            else
            {
                // Use canonical string representation of the absolute path for comparison
                _comparand.Address = _baseAddress.GetComponents(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
            }

            _comparand.Port = _baseAddress.Port;
            _comparand.Scheme = _baseAddress.Scheme;

            if ((_comparand.Port == -1) && (_comparand.Scheme == (object)UriEx.UriSchemeNetTcp))
            {
                // Compensate for the fact that the Uri type doesn't know about our default TCP port number
                _comparand.Port = TransportDefaults.TcpUriDefaultPort;
            }
            _hashCode = _comparand.Address.GetHashCode() ^ _comparand.Port ^ (int)HostNameComparisonMode;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", HostNameComparisonMode, BaseAddress);
        }

        private struct Comparand
        {
            public string Address;
            public int Port;
            public string Scheme;
        }
    }

}