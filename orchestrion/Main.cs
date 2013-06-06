using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Orchestrion.Core;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using Castle.Core.Internal;

namespace Orchestrion
{
    internal sealed class ServerOptions
    {
        public ServerOptions()
        {
            Port = 8082;
            Host = "localhost";
            LogFileDirectory = null;
        }

        public int Port { get; set; }
        public string Host { get; set; }
        public string LogFileDirectory { get; set; }
    }

    internal sealed class UnknownOptionException : Exception
    {
        public UnknownOptionException(string option)
            : base("Unknown option : " + option)
        {
            
        }
    }

    internal sealed class InvalidOptionValueException : Exception
    {
        public InvalidOptionValueException(string option, string value, string additionalMessage = null)
            : base(String.Format("Value '{0}' is invalid for option '{1}'. {2}", value, option, additionalMessage))
        {

        }
    }

    /// <summary>
    /// Orchestrion can be started with/without commandline arguments. If no arguments are specified,
    /// it works with default values. Following arguments are valid
    /// --port VALUE
    /// --host VALUE
    /// --logs DIRECTORY_PATH
    /// </summary>
    public class Program
    {
        private static readonly Dictionary<string, Func<string, string[], ServerOptions, string[]>> commandLineOptions = new Dictionary
            <string, Func<string, string[], ServerOptions, string[]>>
            {
                {"--port", SetPort},
                {"--host", SetHost},
                {"--logs", SetLogsDirectory}                
            };

        static string[] SetPort(string currentOption, string[] args, ServerOptions options)
        {            
            if (args.IsNullOrEmpty())
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a port to be present");

            int port;
            if (!int.TryParse(args.First(), out port))
                throw new InvalidOptionValueException(currentOption, args.First(), "Port should be a number");

            options.Port = port;

            return args.Skip(1).ToArray();
        }

        static string[] SetHost(string currentOption, string[] args, ServerOptions options)
        {
            if (args.IsNullOrEmpty())
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a host name to be present");            

            options.Host = args.First();

            return args.Skip(1).ToArray();
        }

        static string[] SetLogsDirectory(string currentOption, string[] args, ServerOptions options)
        {
            if (args.IsNullOrEmpty())
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a logs directory present");

            string logsDir = args.First();
            if (!Directory.Exists(logsDir))
                throw new InvalidOptionValueException(currentOption, logsDir, "Directory doesn't exists");

            if (!IsDirectoryWritable(logsDir))
                throw new InvalidOptionValueException(currentOption, logsDir, "Directory is not writable");

            options.LogFileDirectory = args.First();

            return args.Skip(1).ToArray();
        }

        static bool IsDirectoryWritable(string path)
        {
            if (!Directory.Exists(path))
                return false;

            try
            {
                string testFile = Path.Combine(path, Guid.NewGuid().ToString());
                using (var writer = File.CreateText(testFile))
                {
                    writer.Write("test");
                }

                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        static void Main(string[] args)
        {
            try
            {
                var serverOptions = ProcessArgs(args);
                ConfigureLogging(serverOptions);
                PrintVersion();

                var server = new Server(serverOptions);
                server.Start();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }            
        }

        static ServerOptions ProcessArgs(string[] args)
        {
            string[] toprocess = args;
            var serverOptions = new ServerOptions();
            while (toprocess.Length > 0)
            {
                string option = toprocess.First();
                if (!commandLineOptions.ContainsKey(option))
                    throw new UnknownOptionException(option);

                toprocess = commandLineOptions[option](option, toprocess.Skip(1).ToArray(), serverOptions);
            }

            return serverOptions;
        }        

        static void ConfigureLogging(ServerOptions options)
        {
            var layout = new PatternLayout("%date %level - %message%newline");
            layout.ActivateOptions();

            var appenders = new List<IAppender>();
            if (!options.LogFileDirectory.IsNullOrEmpty())
            {
                var fileAppender = new FileAppender
                {
                    Layout = layout,
                    Encoding = Encoding.UTF8,
                    File = Path.Combine(options.LogFileDirectory, "orchestrion.log"),
                    AppendToFile = true,
                    LockingModel = new FileAppender.MinimalLock(),
                    ImmediateFlush = true,

                };
                fileAppender.ActivateOptions();
                appenders.Add(fileAppender);
            }
            
            var consoleAppender = new ConsoleAppender
            {
                Layout = layout
            };
            consoleAppender.ActivateOptions();
            appenders.Add(consoleAppender);

            BasicConfigurator.Configure(appenders.ToArray());
        }

        static void PrintVersion()
        {
            try
            {
                var version = ((AssemblyInformationalVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion;
                ILog logger = LogManager.GetLogger(typeof(Program));
                logger.Info("Orchestrion - " + version);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
