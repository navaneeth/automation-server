using System;

namespace Orchestrion.Core
{
    internal sealed class ParameterMissingException : Exception
    {
        private readonly string parameterName;
        private readonly int parameterIndex;

        public ParameterMissingException(string parameterName, int parameterIndex)
        {
            this.parameterIndex = parameterIndex;
            this.parameterName = parameterName;
        }

        public ParameterMissingException(string parameterName)
            : this(parameterName, -1)
        {}

        public override string Message
        {
            get
            {
                if (parameterIndex == -1)
                {
                    return string.Format("Expected parameter '{0}' to contain '{1}', got none", parameterIndex,
                                         parameterName);
                }
                else
                {
                    return String.Format("Expected parameter '{0}', got none", parameterName);
                }
            }
        }
    }
}
