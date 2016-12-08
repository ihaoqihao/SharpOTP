using System;
using System.Collections.Concurrent;
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
            this._tbReply = new ReplyTable();
            this._processor = new MessageProcessor(this._publisher);
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

        #region Node
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
        #endregion

        #region Register Remote Method
        /// <summary>
        /// register
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="actor"></param>
        public void Register<TRequest>(string methodName, SharpOTP.Actor actor)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            this._processor.Register<TRequest>(methodName, actor);
        }
        /// <summary>
        /// register
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="actor"></param>
        public void Register<TRequest, TResult>(string methodName, SharpOTP.Actor actor)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            this._processor.Register<TRequest, TResult>(methodName, actor);
        }
        #endregion

        #region Broadcast
        /// <summary>
        /// broadcast
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="request"></param>
        /// <param name="excludeCurrNode"></param>
        public void Broadcast<TRequest>(string methodName, TRequest request, bool excludeCurrNode = false)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            var arrNodes = this._dispatchPolicy.GetAllNodes();
            if (arrNodes == null || arrNodes.Length == 0) return;

            foreach (var node in arrNodes)
            {
                if (excludeCurrNode && node == this.CurrNode) continue;
                this.CallTo<TRequest>(node, methodName, request);
            }
        }
        /// <summary>
        /// broadcast
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="request"></param>
        /// <param name="excludeCurrNode"></param>
        /// <returns></returns>
        public Task<TResult[]> Broadcast<TRequest, TResult>(string methodName, TRequest request, bool excludeCurrNode = false)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            var nodes = this._dispatchPolicy.GetAllNodes();
            if (nodes == null || nodes.Length == 0) return Task.FromResult(new TResult[0]);

            var arrTask = nodes.Select(n =>
            {
                if (excludeCurrNode && n == this.CurrNode) return null;
                return this.CallTo<TRequest, TResult>(n, methodName, request);
            }).Where(t => t != null).ToArray();
            if (arrTask.Length == 0) return Task.FromResult(new TResult[0]);

            return Task.WhenAll(arrTask);
        }
        #endregion

        #region Call
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="key"></param>
        /// <param name="methodName"></param>
        /// <param name="request"></param>
        public void Call<TRequest>(string key, string methodName, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            this.CallTo<TRequest>(this._dispatchPolicy.GetNode(key), methodName, request);
        }
        /// <summary>
        /// call to
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="toNode"></param>
        /// <param name="methodName"></param>
        /// <param name="request"></param>
        /// <exception cref="ArgumentNullException">toNode is null.</exception>
        /// <exception cref="ArgumentNullException">method name is null.</exception>
        public void CallTo<TRequest>(string toNode, string methodName, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            if (toNode == null) throw new ArgumentNullException("toNode");
            if (methodName == null) throw new ArgumentNullException("methodName");

            //本地节点，直接本地调用
            if (this.CurrNode == toNode)
            {
                this._processor.Call(methodName, request, this.RemoteTimeout);
                return;
            }

            //远程调用
            this._publisher.Publish(new Messaging.Message
            {
                To = toNode,
                Action = Messaging.Actions.Request,
                MethodName = methodName,
                Payload = Thrift.Util.ThriftMarshaller.Serialize(request),
                CreatedTick = DateTimeSlim.UtcNow.Ticks,
                MillisecondsTimeout = this.RemoteTimeout,
            }, this.RemoteTimeout);
        }
        /// <summary>
        /// batch call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="keyFactory"></param>
        /// <param name="arrRequest"></param>
        public void Call<TRequest>(string methodName, Func<TRequest, string> keyFactory, TRequest[] arrRequest)
            where TRequest : Thrift.Protocol.TBase, new()
        {
            if (methodName == null) throw new ArgumentNullException("methodName");
            if (keyFactory == null) throw new ArgumentNullException("keyFactory");
            if (arrRequest == null || arrRequest.Length == 0) throw new ArgumentNullException("arrRequest is null or empty.");

            arrRequest.ToLookup(r => this.CalcNode(keyFactory(r))).ToList().ForEach(c =>
            {
                //本地调用
                if (c.Key == this.CurrNode)
                {
                    this._processor.Call(methodName, c.ToArray(), this.RemoteTimeout);
                    return;
                }

                //远程调用
                this._publisher.Publish(new Messaging.Message
                {
                    To = c.Key,
                    Action = Messaging.Actions.Request,
                    MethodName = methodName,
                    ListPayload = c.Select(Thrift.Util.ThriftMarshaller.Serialize).ToList(),
                    CreatedTick = DateTimeSlim.UtcNow.Ticks,
                    MillisecondsTimeout = this.RemoteTimeout,
                }, this.RemoteTimeout);
            });
        }
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="key"></param>
        /// <param name="methodName"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResult> Call<TRequest, TResult>(string key, string methodName, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            return this.CallTo<TRequest, TResult>(this._dispatchPolicy.GetNode(key), methodName, request);
        }
        /// <summary>
        /// call to
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="toNode"></param>
        /// <param name="methodName"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResult> CallTo<TRequest, TResult>(string toNode, string methodName, TRequest request)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            if (toNode == null) throw new ArgumentNullException("toNode");
            if (methodName == null) throw new ArgumentNullException("methodName");

            //本地节点，直接本地调用
            if (this.CurrNode == toNode)
                return this._processor.Call<TRequest, TResult>(methodName, request, this.RemoteTimeout);

            //远程调用
            var source = new TaskCompletionSource<TResult>();
            var id = Interlocked.Increment(ref this.CORRENTIONID);

            //注册消息回复回调
            var ctx = this._tbReply.Register<TResult>(toNode, id, this.RemoteTimeout,
                ex => source.TrySetException(ex),
                result => source.TrySetResult(result));

            this._publisher.Publish(new Messaging.Message
            {
                To = toNode,
                Action = Messaging.Actions.Request,
                MethodName = methodName,
                ReplyTo = this.CurrNode,
                CorrentionId = id,
                Payload = Thrift.Util.ThriftMarshaller.Serialize(request),
                MillisecondsTimeout = this.RemoteTimeout,
                CreatedTick = DateTimeSlim.UtcNow.Ticks,
            }, this.RemoteTimeout).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    this._tbReply.Remove(id);
                    source.TrySetException(t.Exception.InnerException);
                    return;
                }
                ctx.IsSent = true;
            });

            return source.Task;
        }
        /// <summary>
        /// batch call
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="keyFactory"></param>
        /// <param name="arrRequest"></param>
        /// <returns></returns>
        public Task<TResult[]> Call<TRequest, TResult>(string methodName, Func<TRequest, string> keyFactory, TRequest[] arrRequest)
            where TRequest : Thrift.Protocol.TBase, new()
            where TResult : Thrift.Protocol.TBase, new()
        {
            if (methodName == null) throw new ArgumentNullException("methodName");
            if (keyFactory == null) throw new ArgumentNullException("keyFactory");
            if (arrRequest == null || arrRequest.Length == 0) throw new ArgumentNullException("arrRequest is null or empty.");

            //key:  node
            var dicRequest = new Dictionary<string, List<Tuple<int, TRequest>>>();
            for (int i = 0, l = arrRequest.Length; i < l; i++)
            {
                var request = arrRequest[i];
                var node = this.CalcNode(keyFactory(request));

                List<Tuple<int, TRequest>> list = null;
                if (!dicRequest.TryGetValue(node, out list))
                    dicRequest[node] = list = new List<Tuple<int, TRequest>>();

                list.Add(Tuple.Create(i, request));
            }

            var arrTasks = dicRequest.Select(c =>
            {
                var childRequests = c.Value;

                //本地调用
                if (c.Key == this.CurrNode)
                {
                    return this._processor.Call<TRequest, TResult>(methodName,
                        childRequests.Select(p => p.Item2).ToArray(), this.RemoteTimeout).ContinueWith(t =>
                        {
                            if (t.IsFaulted) throw t.Exception.InnerException;

                            var returnResult = new Dictionary<int, TResult>(t.Result.Length);
                            for (int i = 0, l = t.Result.Length; i < l; i++)
                                returnResult[childRequests[i].Item1] = t.Result[i];

                            return returnResult;
                        });
                }

                //远程调用
                var source = new TaskCompletionSource<Dictionary<int, TResult>>();
                var id = Interlocked.Increment(ref this.CORRENTIONID);

                //注册消息回复回调
                var ctx = this._tbReply.Register<TResult>(c.Key, id, this.RemoteTimeout,
                    ex => source.TrySetException(ex),
                    (TResult[] arrResult) =>
                    {
                        var returnResult = new Dictionary<int, TResult>(arrResult.Length);
                        for (int i = 0, l = arrResult.Length; i < l; i++)
                            returnResult[childRequests[i].Item1] = arrResult[i];

                        source.TrySetResult(returnResult);
                    });

                this._publisher.Publish(new Messaging.Message
                {
                    To = c.Key,
                    Action = Messaging.Actions.Request,
                    MethodName = methodName,
                    ReplyTo = this.CurrNode,
                    CorrentionId = id,
                    ListPayload = childRequests.Select(p => Thrift.Util.ThriftMarshaller.Serialize(p.Item2)).ToList(),
                    MillisecondsTimeout = this.RemoteTimeout,
                    CreatedTick = DateTimeSlim.UtcNow.Ticks,
                }, this.RemoteTimeout).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        this._tbReply.Remove(id);
                        source.TrySetException(t.Exception.InnerException);
                        return;
                    }
                    ctx.IsSent = true;
                });

                return source.Task;
            }).ToArray();

            return Task.WhenAll(arrTasks).ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception.InnerException;

                var dic = new Dictionary<int, TResult>();
                foreach (var childResult in t.Result)
                    foreach (var p in childResult) dic[p.Key] = p.Value;

                return Enumerable.Range(0, arrRequest.Length).Select(i => dic[i]).ToArray();
            });
        }
        #endregion

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
            /// publisher
            /// </summary>
            private readonly Publisher _publisher = null;
            /// <summary>
            /// key:method name
            /// </summary>
            private readonly ConcurrentDictionary<string, Context> _dicCtx =
                new ConcurrentDictionary<string, Context>();
            #endregion

            #region Constructors
            /// <summary>
            /// new
            /// </summary>
            /// <param name="publisher"></param>
            public MessageProcessor(Publisher publisher)
            {
                if (publisher == null) throw new ArgumentNullException("publisher");
                this._publisher = publisher;
            }
            #endregion

            #region API
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <param name="methodName"></param>
            /// <param name="actor"></param>
            /// <exception cref="ArgumentNullException">methodName</exception>
            /// <exception cref="ArgumentNullException">actor</exception>
            /// <exception cref="ApplicationException">the method is registered.</exception>
            public void Register<TRequest>(string methodName, SharpOTP.Actor actor)
                where TRequest : Thrift.Protocol.TBase, new()
            {
                if (methodName == null) throw new ArgumentNullException("methodName");
                if (actor == null) throw new ArgumentNullException("actor");

                if (this._dicCtx.TryAdd(methodName, new Context<TRequest>(methodName, actor))) return;
                throw new ApplicationException(string.Concat("the method [", methodName, "] is registered."));
            }
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="methodName"></param>
            /// <param name="actor"></param>
            /// <exception cref="ArgumentNullException">methodName</exception>
            /// <exception cref="ArgumentNullException">actor</exception>
            /// <exception cref="ApplicationException">the method is registered.</exception>
            public void Register<TRequest, TResult>(string methodName, SharpOTP.Actor actor)
                where TRequest : Thrift.Protocol.TBase, new()
                where TResult : Thrift.Protocol.TBase, new()
            {
                if (methodName == null) throw new ArgumentNullException("methodName");
                if (actor == null) throw new ArgumentNullException("actor");

                if (this._dicCtx.TryAdd(methodName, new Context<TRequest, TResult>(methodName, actor, this._publisher))) return;
                throw new ApplicationException(string.Concat("the method [", methodName, "] is registered."));
            }
            /// <summary>
            /// message
            /// </summary>
            /// <param name="message"></param>
            public void OnMessage(Messaging.Message message)
            {
                Context ctx = null;
                if (this._dicCtx.TryGetValue(message.MethodName, out ctx))
                {
                    ctx.OnMessage(message);
                    return;
                }

                var ex = new Messaging.RemotingException { Error = string.Concat("invalid method:", message.MethodName) };
                Reply(this._publisher, message, ex);
            }
            /// <summary>
            /// call
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <param name="methodName"></param>
            /// <param name="request"></param>
            /// <param name="millisecondsTimeout"></param>
            public void Call<TRequest>(string methodName, TRequest request, int millisecondsTimeout)
            {
                Context ctx = null;
                if (!this._dicCtx.TryGetValue(methodName, out ctx))
                {
                    Trace.TraceError(string.Concat("invalid method:", methodName));
                    return;
                }
                ctx.Call<TRequest>(request, millisecondsTimeout);
            }
            /// <summary>
            /// call
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <param name="methodName"></param>
            /// <param name="arrRequest"></param>
            /// <param name="millisecondsTimeout"></param>
            /// <exception cref="ArgumentNullException">arrRequest</exception>
            public void Call<TRequest>(string methodName, TRequest[] arrRequest, int millisecondsTimeout)
            {
                if (arrRequest == null) throw new ArgumentNullException("arrRequest");
                if (arrRequest.Length == 0) return;

                Context ctx = null;
                if (!this._dicCtx.TryGetValue(methodName, out ctx))
                {
                    Trace.TraceError(string.Concat("invalid method:", methodName));
                    return;
                }
                foreach (var r in arrRequest) ctx.Call<TRequest>(r, millisecondsTimeout);
            }
            /// <summary>
            /// call
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="methodName"></param>
            /// <param name="request"></param>
            /// <param name="millisecondsTimeout"></param>
            /// <returns></returns>
            /// <exception cref="ApplicationException">invalid method</exception>
            public Task<TResult> Call<TRequest, TResult>(string methodName, TRequest request, int millisecondsTimeout)
            {
                Context ctx = null;
                if (!this._dicCtx.TryGetValue(methodName, out ctx))
                {
                    var ex = new ApplicationException(string.Concat("invalid method:", methodName));
                    return Task.Run<TResult>(new Func<Task<TResult>>(() => { throw ex; }));
                }
                return ctx.Call<TRequest, TResult>(request, millisecondsTimeout);
            }
            /// <summary>
            /// call
            /// </summary>
            /// <typeparam name="TRequest"></typeparam>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="methodName"></param>
            /// <param name="arrRequest"></param>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException">arrRequest</exception>
            /// <exception cref="ApplicationException">invalid method</exception>
            public Task<TResult[]> Call<TRequest, TResult>(string methodName, TRequest[] arrRequest, int millisecondsTimeout)
            {
                if (arrRequest == null) throw new ArgumentNullException("arrRequest");
                if (arrRequest.Length == 0) return Task.FromResult(new TResult[0]);

                Context ctx = null;
                if (!this._dicCtx.TryGetValue(methodName, out ctx))
                {
                    var ex = new ApplicationException(string.Concat("invalid method:", methodName));
                    return Task.Run<TResult[]>(new Func<Task<TResult[]>>(() => { throw ex; }));
                }
                return Task.WhenAll(arrRequest.Select(r => ctx.Call<TRequest, TResult>(r, millisecondsTimeout)).ToArray());
            }
            #endregion

            #region Private Methods
            /// <summary>
            /// reply
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="publisher"></param>
            /// <param name="message"></param>
            /// <param name="result"></param>
            static private void Reply<TResult>(Publisher publisher, Messaging.Message message, TResult result)
                where TResult : Thrift.Protocol.TBase, new()
            {
                if (!message.__isset.CorrentionId) return;

                var ticksNow = DateTimeSlim.UtcNow.Ticks;
                var replyMessage = new Messaging.Message
                {
                    To = message.ReplyTo,
                    MethodName = message.MethodName,
                    Action = Messaging.Actions.Response,
                    CorrentionId = message.CorrentionId,
                    Payload = Thrift.Util.ThriftMarshaller.Serialize(result),
                    CreatedTick = ticksNow,
                    MillisecondsTimeout = message.MillisecondsTimeout - Math.Max(0, (int)(ticksNow - message.CreatedTick) / 10000)
                };
                if (replyMessage.MillisecondsTimeout <= 0) return;
                publisher.Publish(replyMessage, replyMessage.MillisecondsTimeout);
            }
            /// <summary>
            /// reply
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="publisher"></param>
            /// <param name="message"></param>
            /// <param name="arrResult"></param>
            static private void Reply<TResult>(Publisher publisher, Messaging.Message message, TResult[] arrResult)
                where TResult : Thrift.Protocol.TBase, new()
            {
                if (!message.__isset.CorrentionId) return;

                var ticksNow = DateTimeSlim.UtcNow.Ticks;
                var replyMessage = new Messaging.Message
                {
                    To = message.ReplyTo,
                    MethodName = message.MethodName,
                    Action = Messaging.Actions.Response,
                    CorrentionId = message.CorrentionId,
                    ListPayload = arrResult.Select(Thrift.Util.ThriftMarshaller.Serialize).ToList(),
                    CreatedTick = ticksNow,
                    MillisecondsTimeout = message.MillisecondsTimeout - Math.Max(0, (int)(ticksNow - message.CreatedTick) / 10000)
                };
                if (replyMessage.MillisecondsTimeout <= 0) return;
                publisher.Publish(replyMessage, replyMessage.MillisecondsTimeout);
            }
            /// <summary>
            /// reply
            /// </summary>
            /// <param name="publisher"></param>
            /// <param name="message"></param>
            /// <param name="ex"></param>
            static private void Reply(Publisher publisher, Messaging.Message message, Exception ex)
            {
                if (!message.__isset.CorrentionId) return;

                var remoteEx = ex as Messaging.RemotingException;
                var ticksNow = DateTimeSlim.UtcNow.Ticks;
                var replyMessage = new Messaging.Message
                {
                    To = message.ReplyTo,
                    MethodName = message.MethodName,
                    Action = Messaging.Actions.Response,
                    CorrentionId = message.CorrentionId,
                    Exception = remoteEx ?? new Messaging.RemotingException { Error = ex.ToString() },
                    CreatedTick = ticksNow,
                    MillisecondsTimeout = message.MillisecondsTimeout - Math.Max(0, (int)(ticksNow - message.CreatedTick) / 10000)
                };
                if (replyMessage.MillisecondsTimeout <= 0) return;
                publisher.Publish(replyMessage, replyMessage.MillisecondsTimeout);
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
                /// method name
                /// </summary>
                public readonly string MethodName;
                /// <summary>
                /// actor
                /// </summary>
                private readonly SharpOTP.Actor _actor;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="methodName"></param>
                /// <param name="actor"></param>
                public Context(string methodName, SharpOTP.Actor actor)
                {
                    if (methodName == null) throw new ArgumentNullException("methodName");
                    if (actor == null) throw new ArgumentNullException("actor");

                    this.MethodName = methodName;
                    this._actor = actor;
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
                /// <param name="millisecondsTimeout"></param>
                public void Call<TRequest>(TRequest request, int millisecondsTimeout)
                {
                    this._actor.Call(request, millisecondsTimeout);
                }
                /// <summary>
                /// call
                /// </summary>
                /// <typeparam name="TRequest"></typeparam>
                /// <typeparam name="TResult"></typeparam>
                /// <param name="request"></param>
                /// <param name="millisecondsTimeout"></param>
                /// <returns></returns>
                public Task<TResult> Call<TRequest, TResult>(TRequest request, int millisecondsTimeout)
                {
                    return this._actor.Call<TResult>(request, millisecondsTimeout);
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
                /// <param name="methodName"></param>
                /// <param name="actor"></param>
                public Context(string methodName, SharpOTP.Actor actor)
                    : base(methodName, actor)
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
                    if (message.Payload != null)
                    {
                        TRequest request;
                        try { request = Thrift.Util.ThriftMarshaller.Deserialize<TRequest>(message.Payload); }
                        catch (Exception ex) { Trace.TraceError(ex.ToString()); return; }
                        base.Call(request, message.MillisecondsTimeout);
                        return;
                    }

                    if (message.ListPayload == null || message.ListPayload.Count == 0)
                    {
                        Trace.TraceError("message.payload is null or empty.");
                        return;
                    }

                    TRequest[] arrRequest = null;
                    try { arrRequest = message.ListPayload.Select(Thrift.Util.ThriftMarshaller.Deserialize<TRequest>).ToArray(); }
                    catch (Exception ex) { Trace.TraceError(ex.ToString()); return; }
                    foreach (var r in arrRequest) base.Call(r, message.MillisecondsTimeout);
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
                /// <param name="methodName"></param>
                /// <param name="actor"></param>
                /// <param name="publisher"></param>
                public Context(string methodName, SharpOTP.Actor actor, Publisher publisher)
                    : base(methodName, actor)
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
                    if (message.Payload != null)
                    {
                        TRequest request;
                        try { request = Thrift.Util.ThriftMarshaller.Deserialize<TRequest>(message.Payload); }
                        catch (Exception ex) { Reply(this._publisher, message, ex); return; }
                        base.Call<TRequest, TResult>(request, message.MillisecondsTimeout).ContinueWith(t =>
                        {
                            if (t.IsFaulted) { Reply(this._publisher, message, t.Exception.InnerException); return; }
                            Reply(this._publisher, message, t.Result);
                        });
                        return;
                    }

                    if (message.ListPayload == null || message.ListPayload.Count == 0)
                    {
                        Reply(this._publisher, message, new Messaging.RemotingException { Error = "message.payload is null or empty." });
                        return;
                    }

                    TRequest[] arrRequest = null;
                    try { arrRequest = message.ListPayload.Select(Thrift.Util.ThriftMarshaller.Deserialize<TRequest>).ToArray(); }
                    catch (Exception ex) { Reply(this._publisher, message, ex); return; }

                    Task.WhenAll(arrRequest.Select(r =>
                        base.Call<TRequest, TResult>(r, message.MillisecondsTimeout)).ToArray())
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted) { Reply(this._publisher, message, t.Exception.InnerException); return; }
                            Reply(this._publisher, message, t.Result);
                        });
                }
                #endregion
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
            /// context.CorrentionId
            /// </summary>
            private readonly ConcurrentDictionary<long, Context> _dicCtx =
                new ConcurrentDictionary<long, Context>();
            /// <summary>
            /// timer
            /// </summary>
            private readonly Timer _timer = null;
            #endregion

            #region Constructors
            /// <summary>
            /// new
            /// </summary>
            public ReplyTable()
            {
                this._timer = new Timer(_ =>
                {
                    if (this._dicCtx.Count == 0) return;
                    var arr = this._dicCtx.ToArray();

                    var dtNow = DateTimeSlim.UtcNow;
                    List<long> listExpired = null;
                    foreach (var child in arr)
                    {
                        if (dtNow < child.Value.ExpireTime) continue;

                        if (listExpired == null) listExpired = new List<long>();
                        listExpired.Add(child.Key);
                    }

                    if (listExpired == null) return;
                    listExpired.ForEach(key =>
                    {
                        Context ctx;
                        if (!this._dicCtx.TryRemove(key, out ctx)) return;
                        ctx.OnTimeout();
                    });
                }, null, 0, 1000);
            }
            #endregion

            #region API
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="toNode"></param>
            /// <param name="correntionId"></param>
            /// <param name="timeout"></param>
            /// <param name="onException"></param>
            /// <param name="onResult"></param>
            /// <returns></returns>
            public Context Register<TResult>(string toNode, long correntionId, int timeout,
                Action<Exception> onException,
                Action<TResult> onResult)
                where TResult : Thrift.Protocol.TBase, new()
            {
                var ctx = new Context<TResult>(toNode, correntionId, timeout, onException, onResult);
                if (this._dicCtx.TryAdd(correntionId, ctx)) return ctx;
                throw new ApplicationException(string.Concat("the ", correntionId.ToString(), " is registered"));
            }
            /// <summary>
            /// register
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="toNode"></param>
            /// <param name="correntionId"></param>
            /// <param name="timeout"></param>
            /// <param name="onException"></param>
            /// <param name="onResult"></param>
            /// <returns></returns>
            public Context Register<TResult>(string toNode, long correntionId, int timeout,
                Action<Exception> onException,
                Action<TResult[]> onResult)
                where TResult : Thrift.Protocol.TBase, new()
            {
                var ctx = new BatchContext<TResult>(toNode, correntionId, timeout, onException, onResult);
                if (this._dicCtx.TryAdd(correntionId, ctx)) return ctx;
                throw new ApplicationException(string.Concat("the ", correntionId.ToString(), " is registered"));
            }
            /// <summary>
            /// on message
            /// </summary>
            /// <param name="message"></param>
            public void OnMessage(Messaging.Message message)
            {
                Context ctx = null;
                if (!this._dicCtx.TryRemove(message.CorrentionId, out ctx)) return;
                ctx.OnMessage(message);
            }
            /// <summary>
            /// remove
            /// </summary>
            /// <param name="correntionId"></param>
            /// <returns></returns>
            public bool Remove(long correntionId)
            {
                Context ctx = null;
                return this._dicCtx.TryRemove(correntionId, out ctx);
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
                /// true is sent.
                /// </summary>
                public volatile bool IsSent = false;
                /// <summary>
                /// toNode
                /// </summary>
                public readonly string ToNode;
                /// <summary>
                /// id
                /// </summary>
                public readonly long CorrentionId;
                /// <summary>
                /// 过期时间
                /// </summary>
                public readonly DateTime ExpireTime;
                /// <summary>
                /// on exception
                /// </summary>
                private readonly Action<Exception> _onException = null;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="toNode"></param>
                /// <param name="correntionId"></param>
                /// <param name="timeout"></param>
                /// <param name="onException"></param>
                public Context(string toNode, long correntionId, int timeout, Action<Exception> onException)
                {
                    if (toNode == null) throw new ArgumentNullException("ToNode");
                    if (timeout < 1) throw new ArgumentOutOfRangeException("timeout");
                    if (onException == null) throw new ArgumentNullException("onException");

                    this.ToNode = toNode;
                    this.CorrentionId = correntionId;
                    this.ExpireTime = DateTimeSlim.UtcNow.AddMilliseconds(timeout);
                    this._onException = onException;
                }
                #endregion

                #region API
                /// <summary>
                /// on message
                /// </summary>
                /// <param name="message"></param>
                public abstract void OnMessage(Messaging.Message message);
                /// <summary>
                /// on timeout
                /// </summary>
                public void OnTimeout()
                {
                    if (this.IsSent) this._onException(new TimeoutException(string.Concat("receive timeout from node:", this.ToNode)));
                    else this._onException(new TimeoutException("pending send timeout"));
                }
                /// <summary>
                /// on exception
                /// </summary>
                /// <param name="ex"></param>
                public void OnException(Exception ex)
                {
                    this._onException(ex);
                }
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
                /// on result
                /// </summary>
                private readonly Action<TResult> _onResult = null;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="toNode"></param>
                /// <param name="correntionId"></param>
                /// <param name="timeout"></param>
                /// <param name="onException"></param>
                /// <param name="onResult"></param>
                public Context(string toNode, long correntionId, int timeout,
                    Action<Exception> onException, Action<TResult> onResult)
                    : base(toNode, correntionId, timeout, onException)
                {
                    if (onResult == null) throw new ArgumentNullException("onResult");
                    this._onResult = onResult;
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
                        base.OnException(message.Exception);
                        return;
                    }

                    TResult result;
                    try { result = Thrift.Util.ThriftMarshaller.Deserialize<TResult>(message.Payload); }
                    catch (Exception ex) { base.OnException(ex); return; }

                    this._onResult(result);
                }
                #endregion
            }
            /// <summary>
            /// batch context
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            public sealed class BatchContext<TResult> : Context
               where TResult : Thrift.Protocol.TBase, new()
            {
                #region Public Members
                /// <summary>
                /// on result
                /// </summary>
                private readonly Action<TResult[]> _onResult = null;
                #endregion

                #region Constructors
                /// <summary>
                /// new
                /// </summary>
                /// <param name="toNode"></param>
                /// <param name="correntionId"></param>
                /// <param name="timeout"></param>
                /// <param name="onException"></param>
                /// <param name="onResult"></param>
                public BatchContext(string toNode, long correntionId, int timeout,
                    Action<Exception> onException, Action<TResult[]> onResult)
                    : base(toNode, correntionId, timeout, onException)
                {
                    if (onResult == null) throw new ArgumentNullException("onResult");
                    this._onResult = onResult;
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
                        base.OnException(message.Exception);
                        return;
                    }

                    TResult[] arrResult;
                    try { arrResult = message.ListPayload.Select(Thrift.Util.ThriftMarshaller.Deserialize<TResult>).ToArray(); }
                    catch (Exception ex) { base.OnException(ex); return; }

                    this._onResult(arrResult);
                }
                #endregion
            }
            #endregion
        }
    }
}