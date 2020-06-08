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
    class Program
    {
        static void Main(string[] args)
        {
            string PillsPath = Path.GetFullPath(args[0]);
            string BlisterPath = Path.GetFullPath(args[0]);
            Console.WriteLine("PillsPath: " + PillsPath);
            Console.WriteLine("BlisterPath: " + BlisterPath);
            if (File.Exists(PillsPath) && File.Exists(BlisterPath))
            {
                MultiBlister structor = new MultiBlister();
                var JSON = structor.CutBlister(PillsPath, BlisterPath);
                Console.WriteLine(JSON);
            }
            else
                Console.WriteLine("path doesn't exist");



           
        }
    }
}
