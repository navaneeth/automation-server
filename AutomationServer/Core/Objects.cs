using System.Collections.Generic;

namespace AutomationServer.Core
{
    internal static class Objects
    {
        private static readonly Dictionary<int, object> objects = new Dictionary<int, object>();
        private static int _nextId = 1;

        public static object Get(int refId)
        {
            return objects[refId];
        }

        public static int Put(object o)
        {
            objects.Add(_nextId, o);
            return _nextId++;
        }

        public static bool HasObject(int refId)
        {
            return objects.ContainsKey(refId);
        }
    }
}