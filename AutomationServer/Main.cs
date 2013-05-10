using System;
using AutomationServer.Core;

namespace WindowsTestServer
{
    public class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server();
            server.Start();
        }
    }
}
