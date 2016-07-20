using RabbitMQ.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
        /// actor
        /// </summary>
        private readonly SharpOTP.Actor _server = null;
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
                VirtualHost = config.VHost,
            };

            this._server = GenServer.Start(this);
            this._server.Call(new ListenMessage());
        }
        #endregion

        #region Actor Callback
        /// <summary>
        /// listen message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(ListenMessage message)
        {
            if (this._disposed) return;
            if (this._connection != null && this._connection.IsOpen) return;

            try { this._connection = this._factory.CreateConnection(); }
            catch (Exception ex)
            {
                this.ReListen();
                Trace.TraceError(ex.ToString());
                return;
            }

            try { base.Model = this._connection.CreateModel(); }
            catch (Exception ex)
            {
                try { this._connection.Close(); }
                catch (Exception ex2) { Trace.TraceError(ex2.ToString()); }
                this._connection = null;
                this.ReListen();
                Trace.TraceError(ex.ToString());
                return;
            }

            try
            {
                base.Model.QueueDeclare(this.QueueName, false, true, true, null);
                base.Model.QueueBind(this.QueueName, this.Exchange, this.QueueName);
                base.Model.BasicConsume(this.QueueName, true, this);
            }
            catch (Exception ex)
            {
                try { this._connection.Close(); }
                catch (Exception ex2) { Trace.TraceError(ex2.ToString()); }
                this._connection = null;
                this.ReListen();
                Trace.TraceError(ex.ToString());
                return;
            }
        }
        /// <summary>
        /// dispose
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<bool> HandleCall(DisposeMessage message)
        {
            this._disposed = true;
            if (this._connection != null)
            {
                try { this._connection.Close(); }
                catch (Exception ex) { Trace.TraceError(ex.ToString()); }
                this._connection = null;
            }
            return true;
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
            if (body == null || body.Length == 0) return;
            this._callback(ThriftMarshaller.Deserialize<Messaging.Message>(body));
        }
        /// <summary>
        /// on channel shutdown
        /// </summary>
        /// <param name="model"></param>
        /// <param name="reason"></param>
        public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            base.HandleModelShutdown(model, reason);
            this.ReListen();
            Trace.TraceError(reason.ToString());
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

        #region Private Methods
        /// <summary>
        /// re listen
        /// </summary>
        private void ReListen()
        {
            Task.Delay(new Random(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0)).Next(1000, 5000))
                .ContinueWith(_ => this._server.Call(new ListenMessage()));
        }
        #endregion

        #region Actor Message
        /// <summary>
        /// listen message
        /// </summary>
        public sealed class ListenMessage
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