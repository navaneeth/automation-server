using System.Collections.Generic;
using System.Net;
using AutomationServer.Extensions;

namespace AutomationServer.Core
{
    /// <summary>
    /// Implements a simple HTTP server which understands the request format
    /// and delegates the command to each command processors
    /// </summary>
    public sealed class Server
    {
        private const int DEFAULT_PORT = 8082;
        private readonly HttpListener listener = new HttpListener();

        public Server(int port)
        {
            listener.Prefixes.Add("http://*:" + port + "/");
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
                
                if ("quit" == command)
                {
                    context.Respond(200, "Bye bye!");
                    execute = false;
                    continue;
                }

                CommandProcessor.Instance.Process(context, command);
            }
        }        
    }
}
