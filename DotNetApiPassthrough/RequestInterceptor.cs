using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DotNetApiPassthrough
{
    public class RequestInterceptor
    {
        private readonly string _baseUrl;

        private static readonly string[] InvalidHeaders =
        {
            "referer",
            "origin",
            "host",
            "sec-",
            "connection",
            "pragma",
            "cache-control",
            "content-length"
        };

        public RequestInterceptor(RequestDelegate next, IConfiguration configuration)
        {
            _baseUrl = configuration["BaseUrl"];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestMethod = context.Request.Method;
            var requestUri = context.Request.Path.Value;
            var queryString = context.Request.QueryString.Value;
            var headers = context.Request.Headers;
            Logger.LogStart(requestMethod, requestUri, queryString, headers);

            try
            {
                using var httpClient = BuildHttpClient(headers);
                var actualUrl = $"{_baseUrl}{requestUri}{queryString}";
                Logger.LogUrl(requestMethod, actualUrl);

                var response = await ExecuteRequest(requestMethod, httpClient, actualUrl, context.Request);
                var responseString = await response.Content.ReadAsStringAsync();
                var responseBytes = await GetResponseBytes(response);
                Logger.LogResponse(requestMethod, actualUrl, response.StatusCode, responseString);

                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType =
                    response.Content.Headers.GetValues("content-type").FirstOrDefault() ?? string.Empty;
                await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
            catch (Exception ex)
            {
                Logger.LogError(requestMethod, requestUri, ex);
                throw;
            }
        }

        private HttpClient BuildHttpClient(IHeaderDictionary headers)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();

            foreach (var header in headers)
            {
                if (InvalidHeaders.Any(h => header.Key.ToLower().Contains(h)))
                {
                    continue;
                }

                if (header.Key.ToLower().Contains("content-type"))
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue(header.Value.ToString()));
                    continue;
                }

                client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString());
            }

            return client;
        }

        private async Task<HttpResponseMessage> ExecuteRequest(
            string requestMethod, HttpClient httpClient, string actualUrl, HttpRequest request)
        {
            using var body = await GetRequestBody(request);

            switch (requestMethod.ToLower())
            {
                case "get": return await httpClient.GetAsync(actualUrl);
                case "delete": return await httpClient.DeleteAsync(actualUrl);
                case "post": return await httpClient.PostAsync(actualUrl, body);
                case "put": return await httpClient.PutAsync(actualUrl, body);
                default:
                    throw new Exception("Invalid request method");
            }
        }

        private async Task<StringContent> GetRequestBody(HttpRequest request)
        {
            var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private async Task<byte[]> GetResponseBytes(HttpResponseMessage response)
        {
            var responseStream = await response.Content.ReadAsStreamAsync();

            await using var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
    }
}