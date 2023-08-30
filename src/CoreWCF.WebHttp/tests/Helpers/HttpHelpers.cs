// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Helpers
{
    public static class HttpHelpers
    {
        public static async Task<(HttpStatusCode statusCode, string content)> GetAsync(Uri baseAddress, string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.BaseAddress = baseAddress;

                HttpResponseMessage response = await httpClient.GetAsync(url);
                string content = await response.Content.ReadAsStringAsync();

                return (response.StatusCode, content);
            }
        }

        public static async Task<(HttpStatusCode statusCode, string content)> GetSslAsync(Uri baseAddress, string url)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using (HttpClient httpClient = new HttpClient(handler))
            {
                httpClient.BaseAddress = baseAddress;

                HttpResponseMessage response = await httpClient.GetAsync(url);
                string content = await response.Content.ReadAsStringAsync();

                return (response.StatusCode, content);
            }
        }

        public static async Task<(HttpStatusCode statusCode, string content)> PostJsonAsync<T>(Uri baseAddress, string url, T data)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.BaseAddress = baseAddress;

                string json = SerializationHelpers.SerializeJson(data);
                using StringContent requestContent = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await httpClient.PostAsync(url, requestContent);
                string responseContent = await response.Content.ReadAsStringAsync();

                return (response.StatusCode, responseContent);
            }
        }

        public static async Task<(HttpStatusCode statusCode, string content)> PostXmlAsync<T>(Uri baseAddress, string url, T data)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.BaseAddress = baseAddress;

                string xml = SerializationHelpers.SerializeXml(data);
                using StringContent requestContent = new StringContent(xml, Encoding.UTF8, "text/xml");

                HttpResponseMessage response = await httpClient.PostAsync(url, requestContent);
                string responseContent = await response.Content.ReadAsStringAsync();

                return (response.StatusCode, responseContent);
            }
        }

        public static async Task<(HttpStatusCode statusCode, byte[] content)> PostRawAsync(Uri baseAddress, string url, byte[] data)
        {
            using (HttpClient client = new())
            {
                client.BaseAddress = baseAddress;

                HttpRequestMessage request = new(HttpMethod.Post, url);
                request.Content = new ByteArrayContent(data);

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsByteArrayAsync();

                return (response.StatusCode, content);
            }
        }
    }
}
