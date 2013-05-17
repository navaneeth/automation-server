using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace AutomationServer.Extensions
{
    public static class HttpListenerContextExtensions
    {
        public static void RespondOk(this HttpListenerContext context, int objectId)
        {
            Respond(context, 200, objectId.ToString(CultureInfo.InvariantCulture));
        }

        public static void RespondOk(this HttpListenerContext context, string message = "")
        {
            Respond(context, 200, message);
        }        

        public static void Respond(this HttpListenerContext context, int httpStatus, string message = "")
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            var bytes = Encoding.UTF8.GetBytes(message);
            response.ContentEncoding = Encoding.UTF8;
            response.StatusCode = httpStatus;
            //response.StatusDescription = message;
            if (String.Compare(request.HttpMethod, "get", StringComparison.OrdinalIgnoreCase) == 0)
            {
                response.ContentLength64 = bytes.LongLength;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            response.OutputStream.Close();
        }
    }
}
