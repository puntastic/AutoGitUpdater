using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AT.AutoGitUpdater
{
    static class ExceptionLogHandler
    {

        public static void LogException(object sender, UnhandledExceptionEventArgs e)
        {
            if (null != e.ExceptionObject)
            {
                File.WriteAllText(Globals.EXCEPTION_LOG_FILE, e.ExceptionObject.ToString());
            }
            else
            {
                File.WriteAllText(Globals.EXCEPTION_LOG_FILE, "null exception object");
            }
            Environment.Exit(1);
        }

        static ExceptionLogHandler()
        {
            //delete the old log then get ready to log any unhandled exceptions
            if (File.Exists(Globals.EXCEPTION_LOG_FILE))
            {
                File.Delete(Globals.EXCEPTION_LOG_FILE);
            }
        }
    }
}
