using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using Newtonsoft.Json;

using ServiceStack.Redis;

namespace AT.AutoGitUpdater
{

    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += ExceptionLogHandler.LogException;

            UpdaterConfiguration updaterConfig = LoadConfig<UpdaterConfiguration>();

            using (AutoGitUpdater agu = new AutoGitUpdater(updaterConfig))
            {
                agu.Start();

                WaitForUserExit(updaterConfig.RedisConfiguration);
            }
        }

        static void WaitForUserExit(RedisConfiguation config)
        {
            for (var key = Console.ReadKey().KeyChar; key != 'x'; key = Console.ReadKey().KeyChar)
            {
                if(key == 'u')
                {
                    using(var client = new RedisClient(config.RedisHost, config.RedisPort))
                        client.PublishMessage(config.RedisMessageChannel, config.RedisMessageMustContain);
                }
            }
        }

        static T LoadConfig<T>()
        {
            var configFilename = File.Exists("UpdaterConfiguration.json") ? "UpdaterConfiguration.json" : "ExampleUpdaterConfiguration.json";
            using (var sr = new StreamReader(configFilename))
            {
                return JsonConvert.DeserializeObject<T>(sr.ReadToEnd());
            }
        }
    }
}

