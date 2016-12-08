using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Example1
{
    class Program
    {
        static public void Main()
        {
            System.Threading.ThreadPool.SetMinThreads(20, 20);
            System.Threading.ThreadPool.SetMaxThreads(100, 100);

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (send, e) =>
            {
                e.SetObserved();
                e.Exception.Flatten().Handle(c =>
                {
                    Console.WriteLine(c.ToString()); return true;
                });
            };
            //listen system trace
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());

            //启动节点127.0.0.1_1和127.0.0.1_2
            RemoteServer.StartNew("otp_test@127.0.0.1_1");
            RemoteServer.StartNew("otp_test@127.0.0.1_2");

            //set
            Console.WriteLine("set...");
            for (int i = 0; i < 10000; i++)
            {
                RedisServer.Set("key_" + i.ToString(), "value_" + i.ToString());
                if (i % 1000 == 0) Console.WriteLine(i);
            }

            //get
            Console.WriteLine("get...");
            for (int i = 0; i < 10000; i++)
            {
                Console.WriteLine("key_" + i.ToString() + " value is " + RedisServer.Get("key_" + i.ToString()).Result.Value);
            }

            //batch get
            Console.WriteLine("batch get...");
            var keys = Enumerable.Range(0, 50).Select(c => "key_" + c.ToString()).ToArray();
            var arrResult = RedisServer.Get(keys).Result;
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var value = arrResult[i].Value;
                Console.WriteLine(key + " value is " + value);
            }

            Console.ReadLine();
            RemoteServer.StopAll();
        }
    }
}