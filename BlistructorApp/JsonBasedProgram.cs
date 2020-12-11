using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;
using Blistructor;

namespace BlistructorApp
{
    class JsonBasedProgram
    {
        static void Main(string[] args)
        {
            string JsonPath = Path.GetFullPath(args[0]);
           // string BlisterPath = Path.GetFullPath(args[1]);
            Console.WriteLine("JsonPath: " + JsonPath);
            if (File.Exists(JsonPath))
            {
                string content = File.ReadAllText(JsonPath);
                MultiBlister structor = new MultiBlister();
                var JSON = structor.CutBlister(content);
                
                if (JSON != null) Console.WriteLine(JSON.ToString());
                else Console.WriteLine("Empty output JSON");
                Console.ReadKey();
            }
            else
                Console.WriteLine("path doesn't exist");           
        }
    }
}
