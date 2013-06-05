using System;
using System.Reflection;
using System.Text;
using Orchestrion.Core;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

namespace Orchestrion
{
    public class Program
    {
        static void Main(string[] args)
        {
            var layout = new PatternLayout("%date %level - %message%newline");
            var fileAppender = new FileAppender
                {
                    Layout = layout,
                    Encoding = Encoding.UTF8,
                    File = "orchestrion.log.txt",
                    AppendToFile = true,
                    LockingModel = new FileAppender.MinimalLock()
                };

            var consoleAppender = new ConsoleAppender
                {
                    Layout = layout
                };

            BasicConfigurator.Configure(fileAppender, consoleAppender);                       
            
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
