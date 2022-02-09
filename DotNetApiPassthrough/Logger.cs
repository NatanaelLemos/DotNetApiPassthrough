using System;
using System.IO;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace DotNetApiPassthrough
{
    public static class Logger
    {
        public static void LogStart(
            string requestMethod, string requestUri, string queryString, IHeaderDictionary headers)
        {
            var headersJson = JsonSerializer.Serialize(headers);
            Log(
                $"Received {requestMethod} request\n" +
                $"- For URI {requestUri}\n" +
                $"- With Query String {queryString}\n" +
                $"- And headers {headersJson}\n\n");
        }

        public static void LogUrl(string requestMethod, string url)
        {
            Log(
                $"Sending {requestMethod} request\n" +
                $"- To {url}\n\n");
        }

        public static void LogResponse(
            string requestMethod, string actualUrl, HttpStatusCode responseStatusCode, string responseString)
        {
            Log(
                $"Received {requestMethod} response\n" +
                $"- For url {actualUrl}\n" +
                $"- With status code {responseStatusCode}\n" +
                $"- And body {responseString}\n\n");
        }

        public static void LogError(string requestMethod, string requestUri, Exception exception)
        {
            Log(
                $"An error occurred on {requestMethod} request\n" +
                $"- To {requestUri}:\n" +
                $"- {exception.Message}\n\n");
        }

        private static void Log(string message)
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            message = $"({date}): {message}";

            WriteToDisk(message);
            WriteToConsole(message);
        }

        private static void WriteToDisk(string message)
        {
            File.AppendAllText("./log.txt", message);
        }

        private static void WriteToConsole(string message)
        {
            if (message.Length > 2300)
            {
                message = message.Substring(0, 2300);
                message = $"{message}...";
            }

            Console.WriteLine(message);
        }
    }
}