using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Thrift.Util;

namespace SharpOTP.Remote
{
    /// <summary>
    /// message publisher
    /// </summary>
    public sealed class Publisher : IDisposable
    {
        #region Private Members
        /// <summary>
        /// server
        /// </summary>
        private readonly SharpOTP.Actor _server = null;
        /// <summary>
        /// <see cref="Worker"/> array
        /// </summary>
        private readonly Worker[] _arrWorkers = null;
        /// <summary>
        /// true is disposed
        /// </summary>
        private bool _isdisposed = false;

        /// <summary>
        /// rabbitMQ connection factory
        /// </summary>
        private readonly ConnectionFactory _factory = null;
        /// <summary>
        /// connection
        /// </summary>
        private IConnection _connection = null;
        /// <summary>
        /// seqId
        /// </summary>
        private int _seqId = -1;
        #endregion

        #region Constructors
        /// <summary>
        /// free
        /// </summary>
        ~Publisher()
        {
            this.Dispose();
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="config"></param>
        public Publisher(Config.RabbitMQ config)
        {
            if (config == null) throw new ArgumentNullException("config");

            this._factory = new ConnectionFactory
            {
                HostName = config.Host,
                Port = config.Port,
                UserName = config.UserName,
                Password = config.Password,
                VirtualHost = config.VHost
            };
            this._arrWorkers = Enumerable.Range(0, 4).Select(_ => new Worker(this, config.Exchange)).ToArray();
            this._server = GenServer.Start(this);
        }
        #endregion

        #region Actor Callback
        /// <summary>
        /// create channel
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<IModel> HandleCall(CreateChannelMessage message)
        {
            if (this._isdisposed)
                throw new ObjectDisposedException("message.publisher");

            if (this._connection == null || !this._connection.IsOpen)
                this._connection = this._factory.CreateConnection();

            return this._connection.CreateModel();
        }
        /// <summary>
        /// dispose
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<bool> HandleCall(DisposeMessage message)
        {
            this._isdisposed = true;
            if (this._connection != null) this._connection.Dispose();
            return true;
        }
        #endregion

        #region IDisposable Members
        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            this._server.Call<bool>(new DisposeMessage()).Wait();
            GC.SuppressFinalize(this);
        }
        #endregion

        #region API
        /// <summary>
        /// create channel
        /// </summary>
        /// <returns></returns>
        private Task<IModel> CreateChannel()
        {
            return this._server.Call<IModel>(new CreateChannelMessage());
        }
        /// <summary>
        /// publish
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Task Publish(Messaging.Message message)
        {
            if (message == null) throw new ArgumentNullException("message");
            var index = (Interlocked.Increment(ref this._seqId) & int.MaxValue) % this._arrWorkers.Length;
            return this._arrWorkers[index].Post(message);
        }
        #endregion

        /// <summary>
        /// worker
        /// </summary>
        public sealed class Worker
        {
            #region Private Members
            private readonly Publisher _publisher = null;
            private readonly string _exchange = null;
            private readonly SharpOTP.Actor _server = null;

            private IModel _channel = null;
            #endregion

            #region Constructors
            /// <summary>
            /// new
            /// </summary>
            /// <param name="publisher"></param>
            /// <param name="exchange"></param>
            public Worker(Publisher publisher, string exchange)
            {
                if (publisher == null) throw new ArgumentNullException("publisher");
                if (string.IsNullOrEmpty(exchange)) throw new ArgumentNullException("exchange");

                this._publisher = publisher;
                this._exchange = exchange;
                this._server = GenServer.Start(this);
            }
            #endregion

            #region Actor Callback
            /// <summary>
            /// publish message
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public async Task<bool> HandleCall(Messaging.Message message)
            {
                if (this._channel == null || this._channel.IsClosed)
                {
                    //create channel.
                    try { this._channel = await this._publisher.CreateChannel(); }
                    catch (Exception ex) { Trace.TraceError(ex.ToString()); }

                    if (this._channel == null || this._channel.IsClosed)
                        throw new ApplicationException("rabbitMQ channel unavailable");
                }

                this._channel.BasicPublish(this._exchange, message.To, null, ThriftMarshaller.Serialize(message));
                return true;
            }
            #endregion

            #region API
            /// <summary>
            /// post
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public Task Post(Messaging.Message message)
            {
                return this._server.Call<bool>(message);
            }
            #endregion
        }

        #region Actor Message
        /// <summary>
        /// create channel message
        /// </summary>
        public sealed class CreateChannelMessage
        {
        }
        /// <summary>
        /// dispose message
        /// </summary>
        public sealed class DisposeMessage
        {
        }
        #endregion
    }
}