	public class UpdaterConfiguration
	{
		public string RedisHost;				            // Redis Host name. Used for connecting to Redis
        public int RedisPort;                               //Redis Server Port
		public string RedisPassword;						// Password to use when connecting to redis host
		public string RedisMessageChannel;					// Redis Message Channel to subscribe to
		public string RedisMessageRepoName;				// A string to look for in the redis message before firing the update routine
		
		public bool KillApplicationBeforeUpdate;			// True if an application needs to be killed before update
		public string ApplicationToKillBeforeUpdate;		// Name of process to kill using Process.GetAllProcessesByName(name).AsParallel().Each(p => p.Kill())
		
		public bool StartApplicationAfterUpdate;			// True if an application needs to be started after update
		public string ApplicationToStartAfterUpdate;		// Filename to start using System.Diagnostics.Process.Start command

        public string GitBashPath;                          // The git bash to use
        public string GitBashEXE;                           // Workaround for an odd bug that would not allow the file to be part of the path
		public string GitWorkingDirectory;					// Directory in which to run git pull command
        public int GitTimeout_Minutes;                       /* Time to wait for the Git before throwing an exception
                                                             * This is necessary as there is little control of the git 
                                                             * executable; It could otherwise sit and wait for input forever*/
        public string GitPullCommand; 						// EG "git pull github master"
	}
