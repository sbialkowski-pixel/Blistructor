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
using log4net;

namespace BlistructorWeb
{
    class Server
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Web");
        private static MultiBlister structor = new MultiBlister();
        static void Main(string[] args)
        {
            Logger.Setup();
            //------------------- define routes -------------------
            //  Route.Before = (rq, rp) => { Console.WriteLine($"Requested: {rq.Url.PathAndQuery}"); return false; };

            Route.Add("/cut?{params}", (rq, rp, args) =>
            {
                string requestId = rq.QueryString.Get("requestId");
                
                if (requestId == null) requestId = Guid.NewGuid().ToString("n").Substring(0, 8); 
                log4net.GlobalContext.Properties["requestId"] = requestId;
            
                log.Info(String.Format("Request for CUT from:{0}", rq.Url.Host.ToString()));
                /*
                string[] req_params = { "requestId", "zero_position_X", "zero_position_Y" };
                Dictionary<string, float> parameters = new Dictionary<strig, float>();
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
                rp.AsText(json.ToString(), "application/json");
                }, "POST");

            Route.Add("/health_check?{params}", (rq, rp, args) =>
            {
                string requestId = rq.QueryString.Get("requestId");
                if (requestId == null) requestId = Guid.NewGuid().ToString("n").Substring(0, 8);
                log4net.GlobalContext.Properties["requestId"] = requestId;
                log.Info(String.Format("Request for HEALTH CHECK from:{0}", rq.Url.Host.ToString()));
                rp.AsText("Cutter server is fine.");
            }, "GET");

            //------------------- start server -------------------           
            var port = 8080;
            log.Info("Running HTTP server on: " + port);

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
