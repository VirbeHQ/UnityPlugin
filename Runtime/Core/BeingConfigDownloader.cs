using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;

namespace Virbe.Core.Handlers
{
    public class BeingConfigDownloader
    {
        private ApiEndpointCoder _endpointCoder;
        private Uri _baseUri;

        public BeingConfigDownloader(Uri settingsUri, string profileId, string profileSecret, string appIdentifer)
        {
            _baseUri = settingsUri;
            _endpointCoder = new ApiEndpointCoder(appIdentifer, profileId, profileSecret);
        }

        public Task<string> DownloadConfig()
        {
            var headers = new Dictionary<string, string>();
            _endpointCoder.UpdateHeaders(headers);
            return Request(_baseUri.AbsoluteUri, HttpMethod.Get, headers, false, null);
        }

        private async Task<string> Request(string endpoint, HttpMethod method, Dictionary<string, string> headers, bool ensureSuccess,
         string body)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json")); //

            var request = new HttpRequestMessage(method, endpoint);
            httpClient.Timeout = new TimeSpan(0, 0, 15);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            var success = false;
            try
            {
                var sendTask = httpClient.SendAsync(request);
                var response = await sendTask;
                success = sendTask.IsCompletedSuccessfully;
                if (ensureSuccess)
                {
                    response.EnsureSuccessStatusCode();
                }
                success |= response.StatusCode == System.Net.HttpStatusCode.OK;
                if (!success)
                {
                    return null;
                }
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception _)
            {
                if (ensureSuccess)
                {
                    throw;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}