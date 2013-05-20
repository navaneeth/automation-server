using System;

namespace Orchestrion.Core
{
    internal sealed class InputException : Exception
    {
        public InputException(string message)
            : base(message)
        {
        }
    }
}
