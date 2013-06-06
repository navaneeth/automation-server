using System;
using System.Net;
using Orchestrion.Extensions;
using log4net;

namespace Orchestrion.Core
{
    /// <summary>
    /// Implements a simple HTTP server which understands the request format
    /// and delegates the command to each command processors
    /// </summary>
    internal sealed class Server
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly ILog logger = LogManager.GetLogger(typeof(Program));
        private readonly ServerOptions options;

        public Server(ServerOptions options)
        {            
            listener.Prefixes.Add("http://" + options.Host +  ":" + options.Port + "/");
            this.options = options;
        }        

        /// <summary>
        /// Starts the server and block the caller. To quit, send the quit command
        /// </summary>
        public void Start()
        {
            try
            {
                listener.Start();
            }
            catch (Exception e)
            {
                logger.Error(e);
                logger.InfoFormat("Port - {0}, Host - {1}, Logs - {2}", options.Port, options.Host, options.LogFileDirectory);
                throw;
            }
            

            logger.InfoFormat("Started at http://{0}:{1}", options.Host, options.Port);
            
            bool execute = true;
            while (execute)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                string command = request.QueryString["command"];
                if (string.IsNullOrEmpty(command))
                {
                    context.Respond(400, "Expected a command, found none");
                    continue;
                }
                
                if ("quit" == command && request.QueryString["ref"] == null)
                {
                    logger.Info("Quitting. Bye bye");
                    context.Respond(200, "Bye bye!");
                    execute = false;
                    continue;
                }

                if ("ping" == command && request.QueryString["ref"] == null)
                {
                    logger.Info("Ping.....");
                    context.Respond(200, "pong");
                    continue;
                }

                logger.Info("Processing " + command);

                CommandProcessor.Instance.Process(context, command);
            }
        }        
    }
}
