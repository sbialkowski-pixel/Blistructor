using System;
using System.Threading;
using SimpleHttp;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;

using Blistructor;

namespace BlistructorWeb
{
    class Server
    {
        private static MultiBlister structor = new MultiBlister();
        static void Main(string[] args)
        {
            //------------------- define routes -------------------
            Route.Before = (rq, rp) => { Console.WriteLine($"Requested: {rq.Url.PathAndQuery}"); return false; };

            Route.Add("/cut", (rq, rp, args) => 
            {
                string content;
                using (var reader = new StreamReader(rq.InputStream, rq.ContentEncoding))
                {
                    content = reader.ReadToEnd();
                }

                var json = structor.CutBlister(content);
                if (json != null) {
                    rp.AsText(json.ToString(), "application/json");
                }
                else
                {
                    rp.AsText("TotalError", "application/json");
                }
            }, "POST");

            Route.Add("/healthcheck", (rq, rp, args) =>
            {
                rp.AsText("Server is fine.");
                Console.WriteLine("Hello healthcheck!");
            }, "GET");

            //------------------- start server -------------------           
            var port = 8080;
            Console.WriteLine("Running HTTP server on: " + port);

            var cts = new CancellationTokenSource();
            var ts = HttpServer.ListenAsync(port, cts.Token, Route.OnHttpRequestAsync, useHttps: false);
            AppExit.WaitFor(cts, ts);
        }
    }
}
