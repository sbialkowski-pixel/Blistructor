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
    class MaskBasedProgram
    {
        static void Main(string[] args)
        {
            string PillsPath = Path.GetFullPath(args[0]);
            string BlisterPath = Path.GetFullPath(args[1]);
            Console.WriteLine("PillsPath: " + PillsPath);
            Console.WriteLine("BlisterPath: " + BlisterPath);
            if (File.Exists(PillsPath) && File.Exists(BlisterPath))
            {
                MultiBlister structor = new MultiBlister();
                var JSON = structor.CutBlister(PillsPath, BlisterPath);
                
                if (JSON != null) Console.WriteLine(JSON.ToString());
                else Console.WriteLine("Empty JSON ");
                Console.ReadKey();
            }
            else
                Console.WriteLine("path doesn't exist");           
        }
    }
}
