using System;
using System.Diagnostics;
using System.Timers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;

namespace AT.AutoGitUpdater.Monitor
{
    class Program
    {
        const int EMAIL_TIME_TO_WAIT_MS = 10 * 60 * 1000; // ten minute delay

        static void Main(string[] args)
        {



            //run the configured git command in the chosen directory, killing the console after the command is run
            List<Process> processes; // = new System.Diagnostics.Process();
            

            bool emailEnabled = true;

            Stopwatch emailTimer = new Stopwatch();

            while (true)
            {
                Process watchedProcess = null;

                //Read all AutoGitUpdater processes and attach to the first one it finds in the same directory as the monotor(this current process)
                processes = new List<Process>(Process.GetProcessesByName("AT.AutoGitUpdater"));
                foreach (Process targetProcess in processes)
                {
                    //NOTE: a 32 bit process cannot acces 64 bit application file paths
                    if (Path.GetDirectoryName(targetProcess.Modules[0].FileName) == Path.GetDirectoryName(Process.GetCurrentProcess().Modules[0].FileName))
                    {
                        Console.WriteLine("\nAttaching to existing AutoGitUpdater");
                        watchedProcess = processes[0];
                    }
                }

                if (watchedProcess == null)
                {
                    Console.WriteLine("\nAutoGitUpdater isn't running: Starting...");
                    try
                    {
                        watchedProcess = Process.Start("AT.AutoGitUpdater.exe");
                        //startUpProcess.BeginOutputReadLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }


                watchedProcess.WaitForExit();

                if (/*watchedProcess.ExitCode != 0 && */File.Exists(AT.AutoGitUpdater.Globals.EXCEPTION_LOG_FILE))
                {
                    if (emailTimer.ElapsedMilliseconds > EMAIL_TIME_TO_WAIT_MS)
                    {
                        emailTimer.Reset();
                        emailEnabled = true;
                    }

                    if (!emailEnabled)
                    {
                        Console.WriteLine("Exception raised but waiting {0} m {1} s to send new email...", (EMAIL_TIME_TO_WAIT_MS - emailTimer.ElapsedMilliseconds) / 60000, ((EMAIL_TIME_TO_WAIT_MS - emailTimer.ElapsedMilliseconds) % 60000) / 1000);
                    }

                    //Construct a subject and message based on the exception thrown and the current aws instance, then send it
                    else
                    {

                        String message = File.ReadAllText(AT.AutoGitUpdater.Globals.EXCEPTION_LOG_FILE);


                        Console.WriteLine("Exception raised: Sending Email.");
                        Console.WriteLine(message);

                        WebClient webClient = new WebClient();
                        string instanceID = "Unkown Instance";
                        string availabilityZone = "Unknown Availability Zone";
                        //mail encode the message
                        message = message.Replace("\n", "<br/>");

                        try
                        {
                            instanceID = webClient.DownloadString("http://169.254.169.254/latest/meta-data/instance-id");
                            availabilityZone = webClient.DownloadString("http://169.254.169.254/latest/meta-data/placement/availability-zone");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }

                        EmailHelper.SendGmail("[AutoGitUpdater] on [" + instanceID + "] at [" + availabilityZone + "] threw Exception",
                            message, true, new MailAddress[] { new MailAddress("ExampleEmail@gmail.com", "Email Owner") }, null, null, null);

                        emailEnabled = false;
                        emailTimer.Start();
                    }
                }

                Process.GetCurrentProcess().WaitForExit(1000);
            }
        }
    }
}
