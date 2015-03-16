# SharpOTP
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Threading.ThreadPool.SetMinThreads(30, 30);
            Do();
            Console.ReadLine();
        }

        static public async Task Do()
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    Console.WriteLine(await TestServer.Post(i.ToString()));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }

    public sealed class TestServer
    {
        static private readonly SharpOTP.Actor _server =
            SharpOTP.GenServer.Start<TestServer>();

        public async Task<string> HandleCall(string message)
        {
            if (message == "1") throw new Exception(message);
            return "hello " + message;
        }

        static public Task<string> Post(string str)
        {
            return _server.Call<string>(str);
        }
    }
}
```
