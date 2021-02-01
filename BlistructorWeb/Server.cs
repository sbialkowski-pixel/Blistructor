using System;
using System.Collections.Generic;
using System.Threading;
using SimpleHttp;
using System.IO;
using System.Linq;
using System.Net;
using System.ComponentModel;
using System.Globalization;

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

            Route.Add("/cut?{params}", (rq, rp, args) =>
            {
                Console.WriteLine(rq.Url.Query.ToString());
                /*
                string[] req_params = { "pixel_spacing", "zero_position_X", "zero_position_Y" };
                Dictionary<string, float> parameters = new Dictionary<string, float>();
                foreach(string param in req_params)
                {
                    var value = rq.QueryString.Get(param);
                    if (value == null) {
                        rp.StatusCode = (int)HttpStatusCode.BadRequest;
                        rp.AsText(String.Format("Bad Request: {0} not in request query", param)); 
                    }
                    else
                    {
                        parameters.Add(param, TryParse<float>(value));
                    }
                }
                */
                string content;
                using (var reader = new StreamReader(rq.InputStream, rq.ContentEncoding))
                {
                    content = reader.ReadToEnd();
                }
               
                //var json = structor.CutBlister(content, parameters["pixelSpacing"], parameters["zero_position_X"], parameters["zero_position_Y"]);
                var json = structor.CutBlister(content);
                if (json != null) {
                    rp.AsText(json.ToString(), "application/json");
                }
                else
                {     
                    rp.StatusCode = (int)HttpStatusCode.InternalServerError;
                    rp.AsText("TotalError", "application/json");
                }
            }, "POST");

            Route.Add("/healthcheck/", (rq, rp, args) =>
            {
                rp.AsText("Cutter server is fine.");
            }, "GET");

            //------------------- start server -------------------           
            var port = 8080;
            Console.WriteLine("Running HTTP server on: " + port);

            var cts = new CancellationTokenSource();
            var ts = HttpServer.ListenAsync(port, cts.Token, Route.OnHttpRequestAsync, useHttps: false);
            AppExit.WaitFor(cts, ts);
        }
 
        public static T TryParse<T>(string inValue)
        {
            TypeConverter converter =
                TypeDescriptor.GetConverter(typeof(T));

            return (T)converter.ConvertFromString(null,
                CultureInfo.InvariantCulture, inValue);
        }
    }
}
