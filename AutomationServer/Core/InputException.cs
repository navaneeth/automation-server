using System;

namespace AutomationServer.Core
{
    internal sealed class InputException : Exception
    {
        public InputException(string message)
            : base(message)
        {
        }
    }
}
