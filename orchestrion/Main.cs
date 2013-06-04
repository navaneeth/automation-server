using System;
using System.Reflection;
using Orchestrion.Core;

namespace Orchestrion
{
    public class Program
    {
        static void Main(string[] args)
        {
            PrintVersion();

            Server server = new Server();
            server.Start();
        }

        static void PrintVersion()
        {
            try
            {
                var version = ((AssemblyInformationalVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion;
                Console.WriteLine("Orchestrion - " + version);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
