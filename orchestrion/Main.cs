using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Orchestrion.Core;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

namespace Orchestrion
{
    internal sealed class ServerOptions
    {
        public ServerOptions()
        {
            Port = 8082;
            Host = "localhost";
            LogFileDirectory = null;
            ConsoleOutput = true;
            ParentProcessId = null;
        }

        public int Port { get; set; }
        public string Host { get; set; }
        public string LogFileDirectory { get; set; }
        public bool ConsoleOutput { get; set; }
        public int? ParentProcessId { get; set; }
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
    /// --console-output TRUE|FALSE
    /// --parent PROCESS_ID - Only required when invoking this within another program. This program will auto quit when parent dies
    /// </summary>
    public class Program
    {
        private static readonly Dictionary<string, Func<string, string[], ServerOptions, string[]>> commandLineOptions = new Dictionary
            <string, Func<string, string[], ServerOptions, string[]>>
            {
                {"--port", SetPort},
                {"--host", SetHost},
                {"--logs", SetLogsDirectory},
                {"--console-output", SetConsoleOutput},
                {"--parent", SetParent}
            };

        static string[] SetPort(string currentOption, string[] args, ServerOptions options)
        {
            if (!args.Any())
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a port to be present");

            int port;
            if (!int.TryParse(args.First(), out port))
                throw new InvalidOptionValueException(currentOption, args.First(), "Port should be a number");

            options.Port = port;

            return args.Skip(1).ToArray();
        }

        static string[] SetHost(string currentOption, string[] args, ServerOptions options)
        {
            if (!args.Any())
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a host name to be present");

            options.Host = args.First();

            return args.Skip(1).ToArray();
        }

        static string[] SetLogsDirectory(string currentOption, string[] args, ServerOptions options)
        {
            if (!args.Any())
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a logs directory present");

            string logsDir = args.First();
            if (!Directory.Exists(logsDir))
                throw new InvalidOptionValueException(currentOption, logsDir, "Directory doesn't exists");

            if (!IsDirectoryWritable(logsDir))
                throw new InvalidOptionValueException(currentOption, logsDir, "Directory is not writable");

            options.LogFileDirectory = args.First();

            return args.Skip(1).ToArray();
        }

        static string[] SetConsoleOutput(string currentOption, string[] args, ServerOptions options)
        {
            if (args.Length == 0)
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a boolean value");

            options.ConsoleOutput = args.First() == Boolean.TrueString;

            return args.Skip(1).ToArray();
        }

        static string[] SetParent(string currentOption, string[] args, ServerOptions options)
        {
            if (args.Length == 0)
                throw new InvalidOptionValueException(currentOption, string.Empty, "Expected a process id");

            int id;
            if (!int.TryParse(args.First(), out id))
                throw new InvalidOptionValueException(currentOption, args.First(), "Process id is not a number");

            options.ParentProcessId = id;

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
                WatchParentProcess(serverOptions);
                LogArgs(args);

                var server = new Server(serverOptions);
                server.Start();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }

        private static void LogArgs(string[] args)
        {
            var logger = LogManager.GetLogger(typeof(Program));
            var command = new StringBuilder();
            command.Append(AppDomain.CurrentDomain.FriendlyName);
            foreach (var s in args)
            {
                command.Append(" ");
                command.Append(s);
            }
            logger.Info(command.ToString());
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
            if (!string.IsNullOrEmpty(options.LogFileDirectory))
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

            if (options.ConsoleOutput)
            {
                var consoleAppender = new ConsoleAppender
                {
                    Layout = layout
                };
                consoleAppender.ActivateOptions();
                appenders.Add(consoleAppender);
            }

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

        static void WatchParentProcess(ServerOptions serverOptions)
        {
            if (!serverOptions.ParentProcessId.HasValue)
                return;

            var watcher = new Thread(() => WatchProcess(serverOptions)){IsBackground = true};
            watcher.Start();
        }

        private static void WatchProcess(ServerOptions serverOptions)
        {
            var logger = LogManager.GetLogger(typeof (Program));
            try
            {
                Debug.Assert(serverOptions.ParentProcessId != null, "serverOptions.ParentProcessId != null");
                var parent = Process.GetProcessById(serverOptions.ParentProcessId.Value);
                parent.WaitForExit();
            }
            catch (Exception e)
            {
                logger.Error("Can't get parent process. Stopped watching." + e.Message);
                return;
            }

            try
            {
                // Parent process exited. Killing this process
                logger.Info("Parent process exited. Quitting..");
                var request =
                    WebRequest.Create(string.Format("http://{0}:{1}/?command=quit", serverOptions.Host,
                                                    serverOptions.Port));
                var response = (HttpWebResponse) request.GetResponse();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    logger.ErrorFormat("Normal exit failed. Server returned - {0}", response.StatusCode);
                    Environment.Exit(1);
                }
            }
            catch (Exception e)
            {
                logger.ErrorFormat("Normal exit failed. {0}. Force killing...", e.Message);
                Environment.Exit(1);
            }
        }
    }
}
