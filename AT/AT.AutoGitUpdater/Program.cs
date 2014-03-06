using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using Newtonsoft.Json;


namespace AT.AutoGitUpdater
{

    class Program
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

        /// <summary>
        /// Update the configured directoy from git, killing/restarting an application if set to do so
        /// </summary>
        /// <param name="config">UpdaterConfiguration object to read settings from</param>
        private static void Update(UpdaterConfiguration config)
        {
            Console.WriteLine("------------- Commencing Update -------------");
            if (config.KillApplicationBeforeUpdate)
            {
                if (!Directory.Exists(Path.GetDirectoryName(config.ApplicationToKillBeforeUpdate)))
                {
                    throw new FileNotFoundException("Could not find the directory of the application to kill");
                }
                Process.GetProcessesByName(config.ApplicationToKillBeforeUpdate).AsParallel().ForAll
                (
                        p =>
                        {
                            if (Path.GetDirectoryName(p.Modules[0].FileName) == Path.GetDirectoryName(config.ApplicationToKillBeforeUpdate))
                            {
                                p.Kill();
                            }
                        }
                );
            }

            //run the configured git command in the chosen directory, killing the console after the command is run
            Process process = new System.Diagnostics.Process();
            ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();

            //startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = config.GitBashPath + config.GitBashEXE;

            startInfo.WorkingDirectory = config.GitBashPath;

            startInfo.UseShellExecute = false;
            startInfo.Arguments = "--login -i";

            // programatically control the bash input
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;

            process.StartInfo = startInfo;

            try
            {
                process.Start();

                process.BeginOutputReadLine();
                StreamWriter bashWriter = process.StandardInput;
                //
                bashWriter.WriteLine("cd " + config.GitWorkingDirectory + "; " + config.GitPullCommand + "; exit");

                process.WaitForExit(config.GitTimeout_Minutes * 1000 * 60);

                if(!process.HasExited)
                {
                    process.Kill();
                    throw new TimeoutException("Git operation failed to complete before timeout of " + config.GitTimeout_Minutes + " Minutes");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(); //be absoulty sure git has closed
                }
                catch (InvalidOperationException) { }//process.Kill() can throw an unrelated exception if it has already exited
                finally
                {
                    throw ex;
                }
            }

            if (config.StartApplicationAfterUpdate)
            {
                Process.Start(config.ApplicationToStartAfterUpdate);
            }

            Console.WriteLine("---------------------------------------------");
        }

        static void Main(string[] args)
        {
            //delete the old log then get ready to log any unhandled exceptions
            if (File.Exists(Globals.EXCEPTION_LOG_FILE))
            {
                File.Delete(Globals.EXCEPTION_LOG_FILE);
            }
            AppDomain.CurrentDomain.UnhandledException += LogException;
            
            UpdaterConfiguration updaterConfig = null;

            string configFileName = File.Exists("UpdaterConfiguration.json") ? "UpdaterConfiguration.json" : "ExampleUpdaterConfiguration.json";

            using (StreamReader configReader = new StreamReader(configFileName))
                updaterConfig = JsonConvert.DeserializeObject<UpdaterConfiguration>(configReader.ReadToEnd());

            using (RedisMonitor rMonitor = new RedisMonitor())
            {
                rMonitor.CreateMonitor(updaterConfig.RedisHost, updaterConfig.RedisPort,
                    updaterConfig.RedisPassword, updaterConfig.RedisMessageChannel, updaterConfig.RedisMessageRepoName);

                // NOTE: Validation string is contained within the redismonitor - when an onmessage event is raised by the Redis subscription
                // It gets checked for the validation string, and if found, passed to the rMonitor's public OnValidatedMessage event,
                // I changed the event name so it should, hopefully be more clear.
                rMonitor.OnValidatedMessage += (channel, message) =>
                {
                    Console.Write("\nRecieved From: {0} => {1}\n", channel, message);
                    Update(updaterConfig);
                };

                //Tasks don't allow 'observed' exceptions without jumping through hoops, also worth noting is that the thread will stop
                //on leaving the rMonitor due to a thrown (but caught) exception stopping the function
                Thread redisThread = new Thread(new ThreadStart(() => rMonitor.Listen()));
                redisThread.Start();

                Console.WriteLine("\nInitialization Complete: Press 'u' to post a git update message to redis, 'q' or 'Q' to quit");
                while (true)
                {
                    ConsoleKeyInfo keyPress = Console.ReadKey();
                    //post the validationString to the subscribed channel; therefore updating this client as well
                    if (keyPress.Key == ConsoleKey.U)
                    {
                        Console.WriteLine();

                        rMonitor.PublishMessage(rMonitor.subscribedChannel, rMonitor.validationString);
                    }

                    if (keyPress.Key == ConsoleKey.Q)
                    {
                        break;
                    }
                }
            }
        }
    }
}

