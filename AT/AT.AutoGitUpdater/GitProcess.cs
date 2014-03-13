using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;

namespace AT.AutoGitUpdater
{

    class GitProcess : IDisposable
    {
        StreamWriter _gitCommandWriter = null;
        string _gitExecutable;
        bool _hasStartedOnce = false;
        Process _gitProcess;

        public GitProcess(string gitExecutable)
        {
            _gitExecutable = gitExecutable;
        }


        public void Start()
        {
            if (_hasStartedOnce)
            {
                throw new InvalidOperationException("This object can only be started once");
            }
            _hasStartedOnce = true;

            ProcessStartInfo gitStartInfo = CreateGitStartInfo(_gitExecutable);
            _gitProcess = ConfigureGitProcess(gitStartInfo);

            StartGitProcess(_gitProcess, out _gitCommandWriter);
        }

        public string SendCommand(string command)
        {
            _gitCommandWriter.Write(command + '\n');

            //TODO: recieve git output
            return null;
        }


        public void CloseGit(int gitTimeout_Minutes)
        {
            SendCommand("exit");
            _gitProcess.WaitForExit(gitTimeout_Minutes * 1000 * 60);

            try
            {
                if (!_gitProcess.HasExited)
                {
                    _gitProcess.Kill();
                    _gitProcess.Close();
                    throw new TimeoutException("Git operation failed to complete before timeout of " + gitTimeout_Minutes + " Minutes");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (!_gitProcess.HasExited)
                    {
                        _gitProcess.Kill(); //be absoulty sure git has closed
                        _gitProcess.Close();
                    }
                }
                catch (InvalidOperationException) { }//process.Kill() can throw an unrelated exception if it has already exited
                finally
                {
                    throw ex;
                }
            }
            finally
            {
                _gitProcess = null;
            }
        }

        private Process ConfigureGitProcess(ProcessStartInfo startInfo)
        {
            Process process = new System.Diagnostics.Process();
            process.StartInfo = startInfo;
            return process;
        }

        private void StartGitProcess(Process targetProcess, out StreamWriter commandWriter)
        {
            targetProcess.Start();
            targetProcess.BeginOutputReadLine();
            commandWriter = targetProcess.StandardInput;
        }

        private ProcessStartInfo CreateGitStartInfo(string gitExecutable)
        {
            ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();

            startInfo.FileName = gitExecutable;

            startInfo.WorkingDirectory = Path.GetDirectoryName(gitExecutable);

            startInfo.UseShellExecute = false;
            startInfo.Arguments = "--login -i";

            // programatically control the bash input
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;

            return startInfo;
        }

        #region Dispose
        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_gitProcess != null)
                    {
                        CloseGit(0);
                    }
                }
                _disposed = true;
            }
        }

        ~GitProcess()
        {
            Dispose(false);
        }
        #endregion
    }
}
