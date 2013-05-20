using Orchestrion.Core;

namespace Orchestrion
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
