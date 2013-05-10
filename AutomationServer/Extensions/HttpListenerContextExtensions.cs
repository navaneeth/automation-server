﻿using System;
using System.Net;
using System.Text;

namespace AutomationServer.Extensions
{
    public static class HttpListenerContextExtensions
    {
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