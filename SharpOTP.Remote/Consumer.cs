using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Thrift.Util;

namespace SharpOTP.Remote
{
    /// <summary>
    ///  message consumer
    /// </summary>
    public sealed class Consumer : DefaultBasicConsumer, IDisposable
    {
        #region Members
        /// <summary>
        /// callback
        /// </summary>
        private Action<Messaging.Message> _callback;

        /// <summary>
        /// rabbitMQ connection factory
        /// </summary>
        private readonly ConnectionFactory _factory = null;
        /// <summary>
        /// rabbitMQ connection
        /// </summary>
        private IConnection _connection = null;

        /// <summary>
        /// true is disposed
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// exchange
        /// </summary>
        public readonly string Exchange;
        /// <summary>
        /// queue name
        /// </summary>
        public readonly string QueueName;
        #endregion

        #region Constructors
        /// <summary>
        /// free
        /// </summary>
        ~Consumer()
        {
            this.Dispose();
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="config"></param>
        /// <param name="callback"></param>
        public Consumer(string queueName, Config.RabbitMQ config,
            Action<Messaging.Message> callback)
        {
            if (string.IsNullOrEmpty(queueName)) throw new ArgumentNullException("queueName");
            if (callback == null) throw new ArgumentNullException("callback");

            this.QueueName = queueName;
            this._callback = callback;
            this.Exchange = config.Exchange;

            this._factory = new ConnectionFactory
            {
                HostName = config.Host,
                Port = config.Port,
                UserName = config.UserName,
                Password = config.Password,
                VirtualHost = config.VHost
            };

            //async start consume
            Task.Run(() => this.StartConsume());
        }
        #endregion

        #region Override Methods
        /// <summary>
        /// Called each time a message arrives for this consumer.
        /// </summary>
        /// <param name="consumerTag"></param>
        /// <param name="deliveryTag"></param>
        /// <param name="redelivered"></param>
        /// <param name="exchange"></param>
        /// <param name="routingKey"></param>
        /// <param name="properties"></param>
        /// <param name="body"></param>
        public override void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
        {
            Messaging.Message message = null;
            try { message = ThriftMarshaller.Deserialize<Messaging.Message>(body); }
            catch { return; }

            this._callback(message);
        }
        /// <summary>
        /// on channel shutdown 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="reason"></param>
        public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            if (this._disposed) return;
            Task.Delay(new Random().Next(100, 500)).ContinueWith(_ =>
                this.StartConsume());
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// start consume
        /// </summary>
        public void StartConsume()
        {
            lock (this)
            {
                if (base.Model == null || base.Model.IsClosed)
                {
                    if (this._connection == null || !this._connection.IsOpen)
                    {
                        try { this._connection = this._factory.CreateConnection(); }
                        catch (Exception ex)
                        {
                            Trace.TraceError(ex.ToString());
                            Task.Delay(new Random().Next(500, 2000)).ContinueWith(_ =>
                                this.StartConsume()); return;
                        }
                    }

                    try { base.Model = this._connection.CreateModel(); }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                        Task.Delay(new Random().Next(500, 2000)).ContinueWith(_ =>
                            this.StartConsume()); return;
                    }
                }

                try
                {
                    base.Model.QueueDeclare(this.QueueName, false, true, true, null);
                    base.Model.QueueBind(this.QueueName, this.Exchange, this.QueueName);
                    base.Model.BasicConsume(this.QueueName, true, this);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    Task.Delay(new Random().Next(500, 2000)).ContinueWith(_ =>
                        this.StartConsume()); return;
                }
            }
        }
        #endregion

        #region IDisposable Members
        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            this._disposed = true;

            var connection = this._connection;
            if (connection != null) connection.Dispose();

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}