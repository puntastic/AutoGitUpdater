using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using ServiceStack.Redis;

namespace AT.AutoGitUpdater
{
    /* struct Message
     {
         public Message(string message1, string message2)
         {
             this.message1 = message1;
             this.message2 = message2;
         }

         public string message1;
         public string message2;
     }*/

    /// <summary>
    /// Handles subscribing to and monitoring a redis channel
    /// </summary>
    class RedisMonitor : IDisposable
    {
        public string host { get; protected set; }
        public string password { get; protected set; }
        public string subscribedChannel { get; protected set; }
        public int port { get; protected set; }

        public string validationString; //Substring to look for in channel messages to before passing along a channel's on message event

        public EventHandler<string> OnValidatedMessage; //use Invoke(string, string) to keep it consistant with the redis OnMessage even


        private RedisClient _redisClient;
        private RedisSubscription _redisSubscription;

        private bool _disposed = false;

        /// <summary>
        /// Create a monitor for the defined host/channel, while cleaning up the hold subscribers
        /// </summary>
        /// <param name="host">Redist Host</param>
        /// <param name="port">Redist port (Note: default redis port is 6379)</param>
        /// <param name="password">Redis password - or blank/null for no password</param>
        /// <param name="channel">Redis channel to subscribe to</param>
        /// <param name="validationString">Substring to look for in channel messages to before passing along a channel's on message event</param>
        public void CreateMonitor(string host, int port, string password, string channel, string validationString)
        {
            if(_redisClient != null)
            {
                _redisClient.Dispose();
                _redisClient = null;

                //can't dispose of redis subscription due to isses of using a single threaded library in a multi threaded way
                _redisSubscription = null;
            }


            this.host = host;
            this.password = password;
            this.subscribedChannel = channel;
            this.validationString = validationString;

            //password omitted from the client call if empty
            if (password == null || password == "")
            {
                _redisClient = new RedisClient(host, port);
            }
            else
            {
                _redisClient = new RedisClient(host, port, password);
            }

            
            _redisClient.ConnectTimeout = 500;

            _redisSubscription = (RedisSubscription)_redisClient.CreateSubscription();

            _redisSubscription.OnMessage += OnSubscriptionMessage;
        }

        public void PublishMessage(string channel, string message)
        {
            //can't post a message while subscribed, so clone the client in order to post, and since this should be done
            //relativly infrequently, it can be released it as soon as it is has fulfilled its purpose

            // NOTE: connections appear to be established only on calls such as PublishMessage and the subscribe() family of methods
            // had to confirm with the documentation, but its where the exceptions are raised.
            using (var clone = _redisClient.CloneClient())
            {
                clone.PublishMessage(channel, message); //blocking
            }
        }

        /// <summary>
        /// Subsribe to the channel and block, while forwarding OnMessage events from the redis subscription
        /// </summary>
        public void Listen()
        {
            //the redis subscription is not thread safe, and a subscription cannot be cancelled outside of the
            //(now blocked) thread that called SubscribeToChannels() - so cleaning up this class on
            //an external thread is going to cause this to throw
            try
            {
                _redisSubscription.SubscribeToChannels(subscribedChannel);
            }
            catch (RedisException  ex)
            {
                if (!_disposed)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Search for {validationString} in {message}, and if found, foward the event to OnValidatedMessage(string, string)
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        private void OnSubscriptionMessage(string channel, string message)
        {
            if (message.Contains(validationString) && OnValidatedMessage != null)
            {
                OnValidatedMessage.Invoke(channel, message);
            }
        }

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
                    if (_redisSubscription != null)
                    {
                        _redisSubscription = null;
                    }


                    _redisClient.Dispose();
                    _redisClient = null;
                }
            }
        }
        ~RedisMonitor()
        {
            Dispose(true);

            Debug.Assert(false, "Call Dispose() instead of relying on the finalizer");
        }
    }
}
