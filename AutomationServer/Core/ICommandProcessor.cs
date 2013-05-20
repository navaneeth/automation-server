using System.Net;
using Orchestrion.CommandProcessor;

namespace Orchestrion.Core
{
    internal interface ICommandProcessor
    {
        void Process(HttpListenerContext context, string command);
    }

    internal static class CommandProcessor
    {
        public static ICommandProcessor Instance 
        { 
            get
            {
                return new WhiteCommandProcessor();
            }
        }
    }
}
