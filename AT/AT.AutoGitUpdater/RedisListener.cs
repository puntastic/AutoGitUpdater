
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using ServiceStack.Redis;
namespace AT.AutoGitUpdater
{
    class RedisListener : IDisposable
    {
        public Action<string, string> OnRedisMessageRecieved;
        Thread redisThread;

        RedisConfiguation _config;
        RedisClient _redisClient;
        RedisSubscription _redisSubscriber;

        //string subscribedChannel;

        public RedisListener(RedisConfiguation config)
        {
            _config = config;
        }

        public void Start()
        {
            _redisClient = CreateClient();

            _redisSubscriber = (RedisSubscription)_redisClient.CreateSubscription();
            _redisSubscriber.OnMessage += (string channel, string message) => OnRedisMessageRecieved.Invoke(channel, message);

            CreateAndStartSubscriptionThread();
        }

        private RedisClient CreateClient()
        {
            RedisClient redisClient;
            //password omitted from the client call if empty
            if (String.IsNullOrEmpty(_config.RedisPassword))
            {
                redisClient = new RedisClient(_config.RedisHost, _config.RedisPort);
            }
            else
            {
                redisClient = new RedisClient(_config.RedisHost, _config.RedisPort, _config.RedisPassword);
            }

            return redisClient;
        }

        private void CreateAndStartSubscriptionThread()
        {
            redisThread = new Thread(new ThreadStart(() => WaitForMessages()));
            redisThread.Start();
        }

        private void WaitForMessages()
        {
            //the redis subscription is not thread safe, and a subscription cannot be cancelled outside of the
            //(now blocked) thread that called SubscribeToChannels() - so cleaning up this class on
            //an external thread is going to cause this to throw
            try
            {
                _redisSubscriber.SubscribeToChannels(_config.RedisMessageChannel);
            }
            catch (RedisException ex)
            {
                if (!_disposed)
                {
                    throw ex;
                }
            }
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
                if (_redisClient != null)
                {
                    _redisClient.Quit();

                    // NOTE: RedisSubscription is disposable, but cannot be disposed with it holding an active subscription 
                    // without it throwing an exception ((and you can't unsubscribe from any thread other than the blocked thread,
                    // this means that the only place that you can unsubscribe is within an onmessage event, triggered by a redis message))
                    // 
                    // Quit() (and dispose) closes the client, which the subscription is reliant on - so there is no 
                    // open subscription connections to worry about; basically, this is a problem for the garbage collector 
                    // to sort out.
                    if (_redisSubscriber != null)
                    {
                        _redisSubscriber = null;
                    }


                    _redisClient.Dispose();
                    _redisClient = null;
                }
            }
        }
        ~RedisListener()
        {
            Dispose(true); ;
        }
    }
}
