using System;
using System.Collections.Generic;
using System.Net;
using AutomationServer.Core;
using AutomationServer.Extensions;
using White.Core;
using White.Core.UIItems;
using White.Core.UIItems.Finders;
using White.Core.UIItems.ListBoxItems;
using White.Core.UIItems.MenuItems;
using White.Core.UIItems.WindowItems;
using White.Core.UIItems.WindowStripControls;

namespace AutomationServer.CommandProcessor
{
    internal sealed class WhiteCommandProcessor : ICommandProcessor
    {
        private readonly Dictionary<string, Action<HttpListenerContext>> commands;
        private object target = null;
        private int currentRefId = -1;

        public WhiteCommandProcessor()
        {
            commands = new Dictionary<string, Action<HttpListenerContext>>
                {
                    {"launch", Launch},
                    {"getwindow", GetWindow},
                    {"getmenubar", GetMenubar},
                    {"getmenuitem", GetMenuItem},
                    {"entertext", EnterText},
                    {"click", Click},
                    {"getcombobox", GetComboBox},
                    {"selecttext", SelectText},  
                    {"getbutton", GetButton},
                    {"close", Close},
                };
        }

        public void Process(HttpListenerContext context, string command)
        {
            if (!commands.ContainsKey(command))
            {
                context.Respond(400, string.Format("'{0}' is a invalid command", command));
                return;
            }

            // Launch doesn't need a ref id
            if (command != "launch")
            {
                if (!TryGetTarget(context))
                    return;
            }

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

            
            var application = target as Application;
            Window window = application.GetWindow(windowTitle);
            int objectId = Objects.Put(window);
            context.Respond(200, objectId.ToString());
        }

        /// <summary>
        /// Enter text to the UI item.
        /// 1 - Text to enter
        /// 2 - Parent window id to wait
        /// </summary>        
        private void EnterText(HttpListenerContext context)
        {
            var textToEnter = context.Request.QueryString["1"];
            var windowId = context.Request.QueryString["2"];
            if (string.IsNullOrEmpty(textToEnter))
            {
                context.Respond(400, "Expected text to enter, found none");
                return;
            }

            if (string.IsNullOrEmpty(windowId))
            {
                context.Respond(400, "Expected a window id, found none");
                return;
            }

            if (!(target is IUIItem))
            {
                context.Respond(400, "Can't execute command on this object");
                return;
            }
            
            if (!Objects.HasObject(Convert.ToInt32(windowId)))
            {
                context.Respond(400, "Invalid window");
                return;
            }

            var window = (Window) Objects.Get(Convert.ToInt32(windowId));
            var uiItem = target as IUIItem;
            uiItem.Enter(textToEnter);

            // This is required because uiItem.Enter() returns before it completes. We need to hold until the operation is done
            window.WaitWhileBusy();
            context.Respond(200);
        }

        private void Close(HttpListenerContext context)
        {
            if (target is Application)
            {
                var app = target as Application;
                app.Close();
                app.Kill();
                app.Dispose();
                Objects.Remove(currentRefId);
                context.Respond(200);
            }
            else if (target is Window)
            {
                var window = target as Window;
                window.Close();
                window.Dispose();
                Objects.Remove(currentRefId);
                context.Respond(200);
            }
            else
            {
                context.Respond(400, "Can't execute command on this object");
            }
        }

        private void GetMenubar(HttpListenerContext context)
        {
            if (target is Window)
            {
                MenuBar menubar = (target as Window).MenuBar;
                int menubarRefId = Objects.Put(menubar);
                context.Respond(200, menubarRefId.ToString());
            }
            else
            {
                context.Respond(400, "Invalid action on " + currentRefId);
            }
        }

        private void GetMenuItem(HttpListenerContext context)
        {
            string menuItemToFind = context.Request.QueryString["1"];
            if (String.IsNullOrEmpty(menuItemToFind))
            {
                context.Respond(400, "Expected a menu item label, found none");
                return;
            }

            if (target is MenuBar)
            {
                var menubar = target as MenuBar;
                Menu menuItem = menubar.MenuItem(menuItemToFind);
                int menuItemId = Objects.Put(menuItem);
                context.Respond(200, menuItemId.ToString());
            }
            else if (target is Menu)
            {
                Menu menuItem = (target as Menu).SubMenu(menuItemToFind);
                int menuItemId = Objects.Put(menuItem);
                context.Respond(200, menuItemId.ToString());
            }
            else
            {
                context.Respond(400, "Invalid action on " + currentRefId);
            }
        }

        private void Click(HttpListenerContext context)
        {
            if (target is IUIItem)
            {
                (target as IUIItem).Click();
                context.Respond(200);
            }
            else
            {
                context.Respond(400, "Invalid action on " + currentRefId);
            }
        }

        private void GetComboBox(HttpListenerContext context)
        {
            if (!(target is Window))
            {
                context.Respond(400, "Invalid action on " + currentRefId);
                return;
            }

            var by = context.Request.QueryString["by"];
            if (string.IsNullOrEmpty(by))
            {
                context.Respond(400, "Expected a 'by' parameter, found none");
                return;
            }

            switch (by)
            {
                case "automationid":
                    var automationId = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(automationId))
                    {
                        context.Respond(400, "Expected automation id, found none");
                        return;
                    }
                    var comboBox = (target as Window).Get<ComboBox>(SearchCriteria.ByAutomationId(automationId));
                    int comboId = Objects.Put(comboBox);
                    context.Respond(200, comboId.ToString());
                    break;
                default:
                    context.Respond(400, "Incorrect value for 'by'");
                    break;
            }
        }

        private void SelectText(HttpListenerContext context)
        {
            string textToSelect = context.Request.QueryString["1"];
            if (String.IsNullOrEmpty(textToSelect))
            {
                context.Respond(400, "Expected text to select, found none");
                return;
            }

            if (target is ListControl)
            {
               (target as ListControl).Select(textToSelect);
                context.Respond(200);
            }
            else
            {
                context.Respond(400, "Invalid action on " + currentRefId);
                return;
            }
        }

        private void GetButton(HttpListenerContext context)
        {
            if (!(target is Window))
            {
                context.Respond(400, "Invalid action on " + currentRefId);
                return;
            }

            var by = context.Request.QueryString["by"];
            if (string.IsNullOrEmpty(by))
            {
                context.Respond(400, "Expected a 'by' parameter, found none");
                return;
            }

            Button button = null;
            switch (by)
            {
                case "automationid":
                    var automationId = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(automationId))
                    {
                        context.Respond(400, "Expected automation id, found none");
                        return;
                    }
                    button = (target as Window).Get<Button>(SearchCriteria.ByAutomationId(automationId));                    
                    break;
                case "text":
                    var text = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(text))
                    {
                        context.Respond(400, "Expected text, found none");
                        return;
                    }
                    button = (target as Window).Get<Button>(SearchCriteria.ByText(text));                    
                    break;
                default:
                    context.Respond(400, "Incorrect value for 'by'");
                    return;
            }

            int buttonId = Objects.Put(button);
            context.Respond(200, buttonId.ToString());
        }


        private bool TryGetTarget(HttpListenerContext context)
        {
            string refIdInRequest = context.Request.QueryString["ref"];
            if (string.IsNullOrEmpty(refIdInRequest))
            {
                context.Respond(400, "Expected ref id, found none");
                return false;
            }

            currentRefId = -1;
            if (!int.TryParse(refIdInRequest, out currentRefId))
            {
                context.Respond(400, "Ref id should be a number");
                return false;
            }

            if (!Objects.HasObject(currentRefId))
            {
                context.Respond(400, "Invalid ref id");
                return false;
            }

            target = Objects.Get(currentRefId);
            return true;
        }
    }
}