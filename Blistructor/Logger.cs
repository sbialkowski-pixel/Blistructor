using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json.Linq;


namespace Blistructor
{
    public class Logger
    {
        private static readonly ILog log = LogManager.GetLogger("Blistructor.Logger");

        public static void Setup()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.Level = Level.Info;

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%5level %logger.%M - %message%newline";
            patternLayout.ActivateOptions();

            //FileAppender - Debug
            FileAppender debug_roller = new FileAppender();

            debug_roller.File = @"D:\PIXEL\Blistructor\debug_cutter.log";
            debug_roller.Layout = patternLayout;
            var levelFilter = new LevelRangeFilter();
            levelFilter.LevelMin = Level.Debug;
            debug_roller.AddFilter(levelFilter);
            //debug_roller.MaxSizeRollBackups = 5;
            //debug_roller.MaximumFileSize = "10MB";
            //debug_roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            //debug_roller.StaticLogFileName = true;
            // debug_roller.LockingModel = new FileAppender.MinimalLock();
            debug_roller.ActivateOptions();
            debug_roller.AppendToFile = false;

            //FileAppender - Production
            FileAppender prod_roller = new FileAppender();
            prod_roller.File = @"D:\PIXEL\Blistructor\cutter.log";
            prod_roller.Layout = patternLayout;
            var levelFilter2 = new LevelRangeFilter();
            levelFilter2.LevelMin = Level.Info;
            prod_roller.AddFilter(levelFilter2);
            //prod_roller.MaxSizeRollBackups = 5;
            //prod_roller.MaximumFileSize = "10MB";
            //prod_roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            //prod_roller.StaticLogFileName = true;
            //  prod_roller.LockingModel = new FileAppender.MinimalLock();
            prod_roller.ActivateOptions();
            prod_roller.AppendToFile = false;


            // Add to root
            // hierarchy.Root.AddAppender(debug_roller);
            hierarchy.Root.AddAppender(prod_roller);
            //hierarchy.Root.Appenders[0].L

            //MemoryAppender memory = new MemoryAppender();
            //memory.ActivateOptions();
            //hierarchy.Root.AddAppender(memory);


            hierarchy.Configured = true;
        }
        public static void ClearAllLogFile()
        {
            RollingFileAppender fileAppender = LogManager.GetRepository()
                      .GetAppenders().FirstOrDefault(appender => appender is RollingFileAppender) as RollingFileAppender;


            if (fileAppender != null && File.Exists(((RollingFileAppender)fileAppender).File))
            {
                string path = ((RollingFileAppender)fileAppender).File;
                log4net.Appender.FileAppender curAppender = fileAppender as log4net.Appender.FileAppender;
                curAppender.File = path;

                FileStream fs = null;
                try
                {
                    fs = new FileStream(path, FileMode.Create);
                }
                catch (Exception ex)
                {
                    log.Error("Could not clear the file log", ex);
                }
                finally
                {
                    if (fs != null)
                    {
                        fs.Close();
                    }

                }
            }
        }
    }

}
