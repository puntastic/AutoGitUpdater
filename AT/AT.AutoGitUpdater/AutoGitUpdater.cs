using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace AT.AutoGitUpdater
{
    class AutoGitUpdater : IDisposable
    {
        UpdaterConfiguration _config;
        bool _started = false;
        RedisListener _redisListener;

        public AutoGitUpdater(UpdaterConfiguration config)
        {
            _config = config;
        }
        public void Start()
        {
            if (_started)
            {
                throw new Exception("Can only start a " + this.GetType().Name + " once"); //for indiscriminate copy-pasta
            }
            _started = true;

            CreateAndStartRedisListener();

            _redisListener.OnRedisMessageRecieved += HandleRedisMessage;
        }

        /// <summary>
        /// Update the configured directoy from git, killing/restarting an application if set to do so
        /// </summary>
        /// <param name="config">UpdaterConfiguration object to read settings from</param>
        private void Update()
        {
            if (_config.TargetApplicationOptions.KillApplicationBeforeUpdate)
            {
                Console.WriteLine("Killing Process");
                KillAllProcessesByNameAndDirectory(_config.TargetApplicationOptions.Directory,
                    _config.TargetApplicationOptions.ApplicationToKillBeforeUpdate);
            }

            RunGit();
            
            if (_config.TargetApplicationOptions.StartApplicationAfterUpdate)
            {
                Console.WriteLine("Starting Process");
                StartProcess(_config.TargetApplicationOptions.Directory,
                    _config.TargetApplicationOptions.ApplicationToStartAfterUpdate);
            }
        }

        private void HandleRedisMessage(string channel, string message)
        {
            if (message.Contains(_config.RedisConfiguration.RedisMessageMustContain))
            {
                Console.Write("\nRecieved From: {0} => {1}\n", channel, message);
                Update();
            }
        }

        private void CreateAndStartRedisListener()
        {
            _redisListener = new RedisListener(_config.RedisConfiguration);
            _redisListener.Start();
        }

        private void RunGit()
        {
            Console.WriteLine("Start of Git");
            using (GitProcess git = new GitProcess(_config.GitOptions.GitBashPath + _config.GitOptions.GitBashEXE))
            {
                git.Start();
                git.SendCommand("cd " + _config.GitOptions.GitWorkingDirectory);
                git.SendCommand(_config.GitOptions.GitPullCommand);
                git.CloseGit(_config.GitOptions.GitTimeout_Minutes);
            }
            Console.WriteLine("End of Git");
        }

        private void KillAllProcessesByNameAndDirectory(string directory, string processName)
        {
            if (!Directory.Exists(directory))
            {
                throw new FileNotFoundException("Could not find the directory of the application to kill");
            }
            Process.GetProcessesByName(processName).AsParallel().ForAll
            (
                    p =>
                    {
                        if (Path.GetDirectoryName(p.Modules[0].FileName) == directory)
                        {
                            p.Kill();
                        }
                    }
            );
        }

        private void StartProcess(string directory, string executableName)
        {
            Process.Start(Path.Combine(directory, executableName));
        }

        
        bool _disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
                if (_redisListener != null)
                {
                    _redisListener.Dispose();
                }
            }
        }
        ~AutoGitUpdater()
        {
            Dispose(true); ;
        }
    }
}
