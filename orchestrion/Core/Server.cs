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
    public sealed class Server
    {
        private const int DEFAULT_PORT = 8082;
        private readonly HttpListener listener = new HttpListener();
        private int port;
        private readonly ILog logger = LogManager.GetLogger(typeof(Program));

        public Server(int port)
        {
            this.port = port;
            listener.Prefixes.Add("http://localhost:" + port + "/");
        }

        public Server()
            : this(DEFAULT_PORT)
        {
        }

        /// <summary>
        /// Starts the server and block the caller. To quit, send the quit command
        /// </summary>
        public void Start()
        {
            listener.Start();

            logger.InfoFormat("Started at http://localhost:{0}", port);            
            
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

                logger.Info("Processing " + command);

                CommandProcessor.Instance.Process(context, command);
            }
        }        
    }
}
