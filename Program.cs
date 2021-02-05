using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var listener = new HttpEventListener();

            string url = "PUT YOUR URL HERE";
            string urlLeftPart = new Uri(url).GetLeftPart(UriPartial.Authority);
            Console.WriteLine(urlLeftPart);

            using var capture = new NetCapture($"test.pcap");

            var cookieContainer = new CookieContainer();
            HttpMessageHandler handler = new SocketsHttpHandler
            {
                PlaintextStreamFilter = (context, token) =>
                {
                    return new ValueTask<Stream>(capture.AddStream(context.PlaintextStream));
                },
                Credentials = new CredentialCache
                {
                    { new Uri(urlLeftPart), "Negotiate", CredentialCache.DefaultNetworkCredentials }
                }
            };
            HttpClient httpClient = new HttpClient(handler, true)
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0");
            try
            {
                Console.WriteLine("Create request " + url);
                HttpResponseMessage response = await httpClient.GetAsync(url);
                Console.WriteLine("Got response");
                Console.WriteLine((int)response.StatusCode + " " + response.StatusCode.ToString());
                File.WriteAllText("a.html", await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }


    internal sealed class HttpEventListener : EventListener
    {
        StreamWriter f = null;
        internal HttpEventListener()
        {
            f = File.CreateText("log.txt");
        }
        // Constant necessary for attaching ActivityId to the events.
        public const EventKeywords TasksFlowActivityIds = (EventKeywords)0x80;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // List of event source names provided by networking in .NET 5.
            if (eventSource.Name.Contains("System.Net"))
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
            // Turn on ActivityId.
            else if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
            {
                // Attach ActivityId to the events.
                //   EnableEvents(eventSource, EventLevel.LogAlways, TasksFlowActivityIds);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var sb = new StringBuilder().Append($"{eventData.TimeStamp:HH:mm:ss.fffffff}  {eventData.ActivityId}.{eventData.RelatedActivityId}  {eventData.EventSource.Name}.{eventData.EventName}(");
            for (int i = 0; i < eventData.Payload?.Count; i++)
            {
                sb.Append(eventData.PayloadNames?[i]).Append(": ").Append(eventData.Payload[i]);
                if (i < eventData.Payload?.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(")");
            //Console.WriteLine(sb.ToString());
            f.WriteLine(sb.ToString());
        }
    }
}