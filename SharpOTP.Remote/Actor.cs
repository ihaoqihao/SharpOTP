using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpOTP.Remote
{
    /// <summary>
    /// remote actor
    /// </summary>
    public sealed class Actor : IDisposable
    {
        #region Members
        /// <summary>
        /// name
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// true表示开启监控
        /// </summary>
        public readonly bool EnableMonitoring;
        /// <summary>
        /// current node
        /// </summary>
        public readonly string CurrNode = null;
        /// <summary>
        /// timeout for remote call(ms)
        /// </summary>
        public readonly int RemoteTimeout;

        private long CORRENTIONID = -1;

        private readonly ReplyTable _tbReply = null;
        private readonly MessageProcessor _processor = null;
        private readonly IMessageDispatchPolicy _dispatchPolicy = null;

        private readonly Publisher _publisher = null;
        private readonly Consumer _consumer = null;
        #endregion

        #region Constructors
        /// <summary>
        /// free
        /// </summary>
        ~Actor()
        {
            this.Dispose();
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="configPath"></param>
        /// <param name="sectionName"></param>
        public Actor(string configPath, string sectionName)
            : this(null, configPath, sectionName)
        {
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="currNode"></param>
        /// <param name="configPath"></param>
        /// <param name="sectionName"></param>
        public Actor(string currNode, string configPath, string sectionName)
        {
            if (string.IsNullOrEmpty(configPath)) throw new ArgumentNullException("configPath");
            if (string.IsNullOrEmpty(sectionName)) throw new ArgumentNullException("sectionName");

            if (!File.Exists(configPath))
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);

            var config = ConfigurationManager.OpenMappedExeConfiguration(
                new ExeConfigurationFileMap { ExeConfigFilename = configPath },
                ConfigurationUserLevel.None).GetSection(sectionName) as Config.OTPSection;

            if (config.Cluster == null) throw new ArgumentNullException("config.Cluster");
            if (config.Cluster.Nodes == null || config.Cluster.Nodes.Count == 0) throw new ArgumentNullException("config.Cluster.Nodes");
            if (string.IsNullOrEmpty(config.Name)) throw new ArgumentNullException("config.Name");
            if (currNode == null) currNode = config.CurrNode;
            if (string.IsNullOrEmpty(currNode)) throw new ArgumentNullException("currNode");

            this.Name = config.Name;
            this.CurrNode = currNode;
            this.EnableMonitoring = config.EnableMonitoring;
            this.RemoteTimeout = config.Cluster.RemoteTimeout;
            this._publisher = new Publisher(config.RabbitMQ);
            this._tbReply = new ReplyTable(this.Name, this.EnableMonitoring);
            this._processor = new MessageProcessor(this.Name, this.EnableMonitoring, this._publisher);
            this._dispatchPolicy = this.CreatePolicy(config.Cluster.DispatchPolicy);
            this._dispatchPolicy.Init(config.Cluster.Nodes.ToArray().Select(c => c.Name).ToArray());

            this._consumer = new Consumer(this.CurrNode, config.RabbitMQ, message =>
            {
                switch (message.Action)
                {
                    case Messaging.Actions.Request: this._processor.OnMessage(message); break;
                    case Messaging.Actions.Response: this._tbReply.OnMessage(message); break;
                }
            });
        }
        #endregion

        #region Private Members
        /// <summary>
        /// create <see cref="IMessageDispatchPolicy"/> impl.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private IMessageDispatchPolicy CreatePolicy(string type)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentNullException("type");

            switch (type)
            {
                case "polling": return new PollingPolicy();
                case "hashMod": return new HashModPolicy();
                case "consistentHash": return new FNV1ConsistentHashPolicy();
                case "consistentHash_fnv1": return new FNV1ConsistentHashPolicy();
                case "consistentHash_ketama": return new KetamaConsistentHashPolicy();
                default: throw new InvalidOperationException("unknow message dispatch policy.");
            }
        }
        #endregion

        #region API
        /// <summary>
        /// get all node name.
        /// </summary>
        /// <returns></returns>
        public string[] GetAllNodes()
        {
            return this._dispatchPolicy.GetAllNodes();
        }
        /// <summary>
        /// 计算指定key返回对应的node
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string CalcNode(string key)
        {
            return this._dispatchPolicy.GetNode(key);
        }
        /// <summary>
        /// 计算指定key返回对应的node
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">keys is null.</exception>
        public Dictionary<string, string> CalcNode(string[] keys)
        {
            if (keys == null) throw new ArgumentNullException("keys");
            if (keys.Length == 0) return new Dictionary<string, string>(0);
            return keys.ToDictionary(k => k, k => CalcNode(k));
        }
        /// <summary>
        /// register
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="code"></param>
        /// <param name="actor"></param>
        /// <returns></returns>
        public bool Register<TRequest>(string code, SharpOTP.Actor actor)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            return this._processor.Register<TRequest>(code, actor);
        }
        /// <summary>
        /// register
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="code"></param>
        /// <param name="actor"></param>
        /// <returns></returns>
        public bool Register<TRequest, TResult>(string code, SharpOTP.Actor actor)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            return this._processor.Register<TRequest, TResult>(code, actor);
        }

        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="key"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task Call<TRequest>(string key, string code, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            return this.CallTo<TRequest>(this._dispatchPolicy.GetNode(key), code, request);
        }
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task Call<TRequest>(string code, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            var nodes = this._dispatchPolicy.GetAllNodes();
            if (nodes == null || nodes.Length == 0) return Task.FromResult(true);
            return Task.WhenAll(nodes.Select(n => this.CallTo<TRequest>(n, code, request)).ToArray());
        }
        /// <summary>
        /// call to
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="toNode"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">toNode is null.</exception>
        /// <exception cref="ArgumentNullException">code is null.</exception>
        public Task CallTo<TRequest>(string toNode, string code, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            if (toNode == null) throw new ArgumentNullException("toNode");
            if (code == null) throw new ArgumentNullException("code");

            //根据key计算本次请求该到哪个节点
            if (this.CurrNode == toNode)//本地节点，直接本地调用
                return this._processor.Get(code).ContinueWith(t =>
                {
                    if (t.IsFaulted) throw t.Exception.InnerException;
                    if (t.Result == null) throw new InvalidOperationException("illegal code, the code unregister actor.");
                    t.Result.Call(request);
                });

            //远程调用
            return this._publisher.Publish(new Messaging.Message
            {
                To = toNode,
                Action = Messaging.Actions.Request,
                Code = code,
                Payload = Thrift.Util.ThriftMarshaller.Serialize(request)
            });
        }

        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="key"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResult> Call<TRequest, TResult>(string key, string code, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            return this.CallTo<TRequest, TResult>(this._dispatchPolicy.GetNode(key), code, request, this.RemoteTimeout);
        }
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="key"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public Task<TResult> Call<TRequest, TResult>(string key, string code, TRequest request, int timeout)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            return this.CallTo<TRequest, TResult>(this._dispatchPolicy.GetNode(key), code, request, timeout);
        }
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResult[]> Call<TRequest, TResult>(string code, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            return this.Call<TRequest, TResult>(code, request, this.RemoteTimeout);
        }
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="key"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResult[]> Call<TRequest, TResult>(string code, TRequest request, int timeout)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            var nodes = this._dispatchPolicy.GetAllNodes();
            if (nodes == null || nodes.Length == 0) return Task.FromResult(new TResult[0]);
            return Task.WhenAll(nodes.Select(n => this.CallTo<TRequest, TResult>(n, code, request, timeout)).ToArray());
        }
        /// <summary>
        /// call to
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="toNode"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResult> CallTo<TRequest, TResult>(string toNode, string code, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            return this.CallTo<TRequest, TResult>(toNode, code, request, this.RemoteTimeout);
        }
        /// <summary>
        /// call to
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="toNode"></param>
        /// <param name="code"></param>
        /// <param name="request"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public Task<TResult> CallTo<TRequest, TResult>(string toNode, string code, TRequest request, int timeout)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            if (toNode == null) throw new ArgumentNullException("toNode");
            if (code == null) throw new ArgumentNullException("code");

            var source = new TaskCompletionSource<TResult>();
            if (this.CurrNode == toNode)//本地节点，直接本地调用
            {
                this._processor.Get(code).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        source.TrySetException(t.Exception.InnerException);
                        return;
                    }
                    if (t.Result == null)
                    {
                        source.TrySetException(new InvalidOperationException("unknow code"));
                        return;
                    }
                    t.Result.Call<TResult>(request).ContinueWith(t2 =>
                    {
                        if (t2.IsFaulted)
                        {
                            source.TrySetException(t2.Exception.InnerException);
                            return;
                        }
                        source.TrySetResult(t2.Result);
                    });
                });
            }
            else
            {
                var id = Interlocked.Increment(ref this.CORRENTIONID);
                //注册消息回复回调
                this._tbReply.Register<TResult>(toNode, id, timeout, result => source.TrySetResult(result),
                    ex => source.TrySetException(ex));

                //远程调用
                this._publisher.Publish(new Messaging.Message
                {
                    To = toNode,
                    Action = Messaging.Actions.Request,
                    Code = code,
                    ReplyTo = this.CurrNode,
                    CorrentionId = id,
                    Payload = Thrift.Util.ThriftMarshaller.Serialize(request)
                }).ContinueWith(t =>
                {
                    this._tbReply.Remove(id);
                    source.TrySetException(t.Exception.InnerException);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }

            return source.Task;
        }
        #endregion

        #region IDisposable Members
        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            this._consumer.Dispose();
            this._publisher.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion

        /// <summary>
        /// message processor
        /// </summary>
        public sealed class MessageProcessor
        {
            #region Private Members
            /// <summary>
            /// server
            /// </summary>
            private readonly SharpOTP.Actor _server = null;
            /// <summary>
            /// publisher
            /// </summary>
            private readonly Publisher _publisher = null;
            /// <summary>
            /// key:code
            /// </summary>
            private readonly Dictionary<string, Context> _dicCtx =
                new Dictionary<string, Context>();
            #endregion

            #region Constructors
            /// <summary>
            /// new
            /// </summary>
            /// <param name="actorName"></param>
            /// <param name="enableMonitoring"></param>
            /// <param name="publisher"></param>
            public MessageProcessor(string actorName, bool enableMonitoring, Publisher publisher)
            {
                if (publisher == null) throw new ArgumentNullException("publisher");

                this._publisher = publisher;
                this._server = GenServer.Start(this,
                    string.Concat(actorName, ".Processor"), 1, enableMonitoring);
            }
            #endregion

            #region Actor Callback
            /// <summary>
            /// register
            /// </summary>
            /// <param name="context"></param>
            /// <returns></returns>
            public async Task HandleCall(Context context)
            {
                this._dicCtx[context.Code] = context;
            }
            /// <summary>
            /// on message
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public async Task HandleCall(Messaging.Message message)
            {
                Context ctx = null;
                if (this._dicCtx.TryGetValue(message.Code, out ctx))
                {
                    ctx.OnMessage(message);
                    return;
                }

                if (string.IsNullOrEmpty(message.ReplyTo)) return;
                this._publisher.Publish(new Messaging.Message
                {
                    Action = Messaging.Actions.Response,
                    To = message.ReplyTo,
                    CorrentionId = message.CorrentionId,
                    Code = message.Code,
                    Exception = new Messaging.RemotingException
                    {
                        Error = "unknow code"
                    }
                });
            }
            /// <summary>
            /// get actor by code
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public async Task<SharpOTP.Actor> HandleCall(GetActorMessage message)
            {
                Context ctx = null;
                if (this._dicCtx.TryGetValue(message.Code, out ctx)) return ctx.Actor;
                return null;
            }
            #endregion

            #region API
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <param name="code"></param>
            /// <param name="actor"></param>
            /// <returns></returns>
            public bool Register<TRequest>(string code, SharpOTP.Actor actor)
                where TRequest : Thrift.Protocol.TBase, new()
            {
                if (code == null) throw new ArgumentNullException("code");
                if (actor == null) throw new ArgumentNullException("actor");

                return this._server.Call(new Context<TRequest>(code, actor));
            }
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="code"></param>
            /// <param name="actor"></param>
            /// <returns></returns>
            public bool Register<TRequest, TResult>(string code, SharpOTP.Actor actor)
                where TRequest : Thrift.Protocol.TBase, new()
                where TResult : Thrift.Protocol.TBase, new()
            {
                if (code == null) throw new ArgumentNullException("code");
                if (actor == null) throw new ArgumentNullException("actor");

                return this._server.Call(new Context<TRequest, TResult>(code, actor, this._publisher));
            }
            /// <summary>
            /// message
            /// </summary>
            /// <param name="message"></param>
            public void OnMessage(Messaging.Message message)
            {
                this._server.Call(message);
            }
            /// <summary>
            /// get actor by code
            /// </summary>
            /// <param name="code"></param>
            /// <returns></returns>
            public Task<SharpOTP.Actor> Get(string code)
            {
                return this._server.Call<SharpOTP.Actor>(new GetActorMessage(code));
            }
            #endregion

            #region Context
            /// <summary>
            /// context
            /// </summary>
            public abstract class Context
            {
                #region Public Members
                /// <summary>
                /// code
                /// </summary>
                public readonly string Code;
                /// <summary>
                /// actor
                /// </summary>
                public readonly SharpOTP.Actor Actor;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="code"></param>
                /// <param name="actor"></param>
                public Context(string code, SharpOTP.Actor actor)
                {
                    if (code == null) throw new ArgumentNullException("code");
                    if (actor == null) throw new ArgumentNullException("actor");

                    this.Code = code;
                    this.Actor = actor;
                }
                #endregion

                #region API
                /// <summary>
                /// on message
                /// </summary>
                /// <param name="message"></param>
                public abstract void OnMessage(Messaging.Message message);
                /// <summary>
                /// call
                /// </summary>
                /// <typeparam name="TRequest"></typeparam>
                /// <param name="request"></param>
                /// <returns></returns>
                public bool Call<TRequest>(TRequest request)
                {
                    return this.Actor.Call(request);
                }
                /// <summary>
                /// call
                /// </summary>
                /// <typeparam name="TRequest"></typeparam>
                /// <typeparam name="TResult"></typeparam>
                /// <param name="request"></param>
                /// <returns></returns>
                public Task<TResult> Call<TRequest, TResult>(TRequest request)
                {
                    return this.Actor.Call<TResult>(request);
                }
                #endregion
            }

            /// <summary>
            /// context
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            public class Context<TRequest> : Context
                where TRequest : Thrift.Protocol.TBase, new()
            {
                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="code"></param>
                /// <param name="actor"></param>
                public Context(string code, SharpOTP.Actor actor)
                    : base(code, actor)
                {
                }
                #endregion

                #region API
                /// <summary>
                /// on message
                /// </summary>
                /// <param name="message"></param>
                public override void OnMessage(Messaging.Message message)
                {
                    TRequest request;
                    try { request = Thrift.Util.ThriftMarshaller.Deserialize<TRequest>(message.Payload); }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                        return;
                    }
                    this.Actor.Call(request);
                }
                #endregion
            }

            /// <summary>
            /// context
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <typeparam name="TResult"></typeparam>
            public class Context<TRequest, TResult> : Context
                where TRequest : Thrift.Protocol.TBase, new()
                where TResult : Thrift.Protocol.TBase, new()
            {
                #region Private Members
                private readonly Publisher _publisher = null;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="code"></param>
                /// <param name="actor"></param>
                /// <param name="publisher"></param>
                public Context(string code, SharpOTP.Actor actor, Publisher publisher)
                    : base(code, actor)
                {
                    if (publisher == null) throw new ArgumentNullException("publisher");
                    this._publisher = publisher;
                }
                #endregion

                #region API
                /// <summary>
                /// on message
                /// </summary>
                /// <param name="message"></param>
                public override void OnMessage(Messaging.Message message)
                {
                    TRequest request;
                    try { request = Thrift.Util.ThriftMarshaller.Deserialize<TRequest>(message.Payload); }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                        return;
                    }

                    this.Actor.Call<TResult>(request).ContinueWith(t =>
                    {
                        if (string.IsNullOrEmpty(message.ReplyTo)) return;

                        var replyMessage = new Messaging.Message
                        {
                            To = message.ReplyTo,
                            Code = message.Code,
                            Action = Messaging.Actions.Response,
                            CorrentionId = message.CorrentionId,
                        };
                        if (t.IsFaulted)
                        {
                            var ex = t.Exception.InnerException;
                            if (ex is Messaging.RemotingException) replyMessage.Exception = ex as Messaging.RemotingException;
                            else replyMessage.Exception = new Messaging.RemotingException { Error = ex.ToString() };
                        }
                        else replyMessage.Payload = Thrift.Util.ThriftMarshaller.Serialize(t.Result);

                        //send the reply message
                        this._publisher.Publish(replyMessage);
                    });
                }
                #endregion
            }
            #endregion

            #region Actor Message
            /// <summary>
            /// get actor message
            /// </summary>
            public sealed class GetActorMessage
            {
                /// <summary>
                /// code
                /// </summary>
                public readonly string Code;

                /// <summary>
                /// new
                /// </summary>
                /// <param name="code"></param>
                public GetActorMessage(string code)
                {
                    if (code == null) throw new ArgumentNullException(code);
                    this.Code = code;
                }
            }
            #endregion
        }

        /// <summary>
        /// reply table
        /// </summary>
        public sealed class ReplyTable
        {
            #region Members
            /// <summary>
            /// server
            /// </summary>
            private readonly SharpOTP.Actor _server = null;
            /// <summary>
            /// context.CorrentionId
            /// </summary>
            private readonly Dictionary<long, Context> _dicCtx =
                new Dictionary<long, Context>();
            /// <summary>
            /// timer
            /// </summary>
            private readonly Timer _timer = null;
            #endregion

            #region Constructors
            /// <summary>
            /// new
            /// </summary>
            /// <param name="actorName"></param>
            /// <param name="enableMonitoring"></param>
            public ReplyTable(string actorName, bool enableMonitoring)
            {
                this._server = GenServer.Start(this,
                    string.Concat(actorName, ".reply"), 1, enableMonitoring);
                this._timer = new Timer(_ => this._server.Call(new TimeMessage()), null, 0, 1000);
            }
            #endregion

            #region Actor Callback
            /// <summary>
            /// register
            /// </summary>
            /// <param name="context"></param>
            /// <returns></returns>
            public async Task HandleCall(Context context)
            {
                this._dicCtx[context.CorrentionId] = context;
            }
            /// <summary>
            /// reply
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public async Task HandleCall(Messaging.Message message)
            {
                Context ctx = null;
                if (!this._dicCtx.TryGetValue(message.CorrentionId, out ctx)) return;

                this._dicCtx.Remove(message.CorrentionId);
                ctx.OnMessage(message);
            }
            /// <summary>
            /// remove
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public async Task HandleCall(RemoveMessage message)
            {
                this._dicCtx.Remove(message.CorrentionId);
            }
            /// <summary>
            /// time for check timeout
            /// </summary>
            /// <param name="context"></param>
            /// <returns></returns>
            public async Task HandleCall(TimeMessage context)
            {
                var dtNow = DateTimeSlim.UtcNow;
                var arr = this._dicCtx.Values
                    .Where(c => dtNow.Subtract(c.CurrTime).TotalMilliseconds >= c.Timeout)
                    .ToArray();

                if (arr.Length == 0) return;
                foreach (var ctx in arr)
                {
                    this._dicCtx.Remove(ctx.CorrentionId);
                    ctx.Error(new TimeoutException(string.Concat("from node:", ctx.To)));
                }
            }
            #endregion

            #region API
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="to"></param>
            /// <param name="correntionId"></param>
            /// <param name="timeout"></param>
            /// <param name="callback"></param>
            /// <param name="error"></param>
            public void Register<TResult>(string to, long correntionId, int timeout,
                Action<TResult> callback, Action<Exception> error)
                where TResult : Thrift.Protocol.TBase, new()
            {
                this._server.Call(new Context<TResult>(to, correntionId, timeout, callback, error));
            }
            /// <summary>
            /// on message
            /// </summary>
            /// <param name="message"></param>
            public void OnMessage(Messaging.Message message)
            {
                this._server.Call(message);
            }
            /// <summary>
            /// remove
            /// </summary>
            /// <param name="id"></param>
            public void Remove(long id)
            {
                this._server.Call(new RemoveMessage(id));
            }
            #endregion

            #region Context
            /// <summary>
            /// context
            /// </summary>
            public abstract class Context
            {
                #region Public Members
                /// <summary>
                /// to
                /// </summary>
                public readonly string To;
                /// <summary>
                /// id
                /// </summary>
                public readonly long CorrentionId;
                /// <summary>
                /// timeout, ms
                /// </summary>
                public readonly int Timeout;
                /// <summary>
                /// current time
                /// </summary>
                public readonly DateTime CurrTime = DateTimeSlim.UtcNow;
                /// <summary>
                /// error
                /// </summary>
                public readonly Action<Exception> Error;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="to"></param>
                /// <param name="correntionId"></param>
                /// <param name="timeout"></param>
                /// <param name="error"></param>
                public Context(string to, long correntionId, int timeout, Action<Exception> error)
                {
                    if (to == null) throw new ArgumentNullException("to");
                    if (error == null) throw new ArgumentNullException("error");
                    if (timeout < 1) throw new ArgumentOutOfRangeException("timeout");

                    this.To = to;
                    this.CorrentionId = correntionId;
                    this.Timeout = timeout;
                    this.Error = error;
                }
                #endregion

                #region API
                /// <summary>
                /// on message
                /// </summary>
                /// <param name="message"></param>
                public abstract void OnMessage(Messaging.Message message);
                #endregion
            }
            /// <summary>
            /// context
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            public sealed class Context<TResult> : Context
                where TResult : Thrift.Protocol.TBase, new()
            {
                #region Public Members
                /// <summary>
                /// callback
                /// </summary>
                public readonly Action<TResult> Callback;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="to"></param>
                /// <param name="correntionId"></param>
                /// <param name="timeout"></param>
                /// <param name="callback"></param>
                /// <param name="error"></param>
                public Context(string to, long correntionId, int timeout,
                    Action<TResult> callback, Action<Exception> error)
                    : base(to, correntionId, timeout, error)
                {
                    if (callback == null) throw new ArgumentNullException("callback");
                    this.Callback = callback;
                }
                #endregion

                #region API
                /// <summary>
                /// on message
                /// </summary>
                /// <param name="message"></param>
                public override void OnMessage(Messaging.Message message)
                {
                    if (message.Exception != null)
                    {
                        this.Error(message.Exception);
                        return;
                    }

                    try { this.Callback(Thrift.Util.ThriftMarshaller.Deserialize<TResult>(message.Payload)); }
                    catch (Exception ex) { this.Error(ex); }
                }
                #endregion
            }
            #endregion

            #region Actor Message
            /// <summary>
            /// remove message
            /// </summary>
            public sealed class RemoveMessage
            {
                /// <summary>
                /// correntionId
                /// </summary>
                public readonly long CorrentionId;

                /// <summary>
                /// new
                /// </summary>
                /// <param name="id"></param>
                public RemoveMessage(long id)
                {
                    this.CorrentionId = id;
                }
            }
            #endregion
        }
    }
}