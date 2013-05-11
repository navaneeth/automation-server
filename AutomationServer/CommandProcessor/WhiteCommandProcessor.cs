using System;
using System.Collections.Generic;
using System.Net;
using AutomationServer.Core;
using AutomationServer.Extensions;
using White.Core;
using White.Core.UIItems;
using White.Core.UIItems.WindowItems;

namespace AutomationServer.CommandProcessor
{
    internal sealed class WhiteCommandProcessor : ICommandProcessor
    {
        private readonly Dictionary<string, Action<HttpListenerContext>> commands;
        private object target = null;

        public WhiteCommandProcessor()
        {
            commands = new Dictionary<string, Action<HttpListenerContext>>
                {
                    {"launch", Launch},
                    {"getwindow", GetWindow},
                    {"entertext", EnterText},
                };
        }

        public void Process(HttpListenerContext context, string command)
        {
            if (!commands.ContainsKey(command))
            {
                context.Respond(400, string.Format("'{0}' is a invalid command", command));
                return;
            }

            if (!TryGetTarget(context))
                return;

            try
            {
                var handler = commands[command];
                handler(context);
            }
            catch (Exception e)
            {
                context.Respond(500, e.Message);
            }
        }

        private void Launch(HttpListenerContext context)
        {
            var applicationPath = context.Request.QueryString["1"];
            if (string.IsNullOrEmpty(applicationPath))
            {
                context.Respond(400, "Expected application path to launch, found none");
                return;
            }

            Application application = Application.Launch(applicationPath);
            int objectId = Objects.Put(application);
            context.Respond(200, objectId.ToString());
        }

        private void GetWindow(HttpListenerContext context)
        {
            var windowTitle = context.Request.QueryString["1"];
            if (string.IsNullOrEmpty(windowTitle))
            {
                context.Respond(400, "Expected window title, found none");
                return;
            }

            if (!(target is Application))
            {
                context.Respond(400, "Can't execute command on this object");
                return;
            }

            try
            {
                var application = target as Application;
                Window window = application.GetWindow(windowTitle);
                int objectId = Objects.Put(window);
                context.Respond(200, objectId.ToString());
            }
            catch (Exception e)
            {
                context.Respond(500, e.Message);
            }
        }

        private void EnterText(HttpListenerContext context)
        {
            var textToEnter = context.Request.QueryString["1"];
            if (string.IsNullOrEmpty(textToEnter))
            {
                context.Respond(400, "Expected text to enter, found none");
                return;
            }

            if (!(target is IUIItem))
            {
                context.Respond(400, "Can't execute command on this object");
                return;
            }

            try
            {
                var uiItem = target as UIItem;
                uiItem.Enter(textToEnter);
            }
            catch (Exception e)
            {
                context.Respond(500, e.Message);
            }
        }

        private bool TryGetTarget(HttpListenerContext context)
        {
            string refIdInRequest = context.Request.QueryString["ref"];
            if (string.IsNullOrEmpty(refIdInRequest))
            {
                context.Respond(400, "Expected ref id, found none");
                return false;
            }

            int refId = -1;
            if (!int.TryParse(refIdInRequest, out refId))
            {
                context.Respond(400, "Ref id should be a number");
                return false;
            }

            if (!Objects.HasObject(refId))
            {
                context.Respond(400, "Invalid ref id");
                return false;
            }

            target = Objects.Get(refId);
            return true;
        }
    }
}