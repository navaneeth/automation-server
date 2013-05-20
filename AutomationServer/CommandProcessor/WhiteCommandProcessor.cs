using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
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
        private readonly Dictionary<string, Action> commands;
        private object target;
        private int currentRefId = -1;
        private HttpListenerContext context;

        public WhiteCommandProcessor()
        {
            commands = new Dictionary<string, Action>
                {
                    {"launch", Launch},
                    {"attach", AttachToExistingProcess},
                    {"getwindow", GetWindow},

                    {"getmenubar", GetMenubar},
                    {"gettitle", GetTitle},
                    {"isenabled", IsEnabled},
                    {"doubleclick", DoubleClick},
                    {"isoffscreen", IsOffScreen},
                    {"setfocus", SetFocus},
                    {"isfocused", IsFocused},

                    {"getmenuitem", GetMenuItem},
                    {"entertext", EnterText},
                    {"click", Click},                    
                    
                    {"getcombobox", GetComboBox},
                    {"selecttext", SelectText},
                    {"iseditable", IsEditable},
                    {"getselecteditem", GetSelectedItem},
                    {"getlistitems", GetListItems},

                    // ListItem
                    {"gettext", GetText},
                    {"checklistitem", CheckListItem},
                    {"unchecklistitem", UnCheckListItem},
                    {"selectlistitem", SelectListItem},
                    {"isselected", IsSelected},
                    {"ischecked", IsChecked},
  
                    {"getbutton", GetButton},
                    {"close", Close},
                };
        }

        public void Process(HttpListenerContext c, string command)
        {
            context = c;

            if (!commands.ContainsKey(command))
            {
                context.Respond(400, string.Format("Unknown command - '{0}'", command));
                return;
            }

            // Launch doesn't need a ref id
            if (command != "launch" && command != "attach")
            {
                if (!TryGetTarget())
                    return;
            }

            try
            {
                var handler = commands[command];
                handler();
            }
            catch (ParameterMissingException e)
            {
                context.Respond(400, e.Message);
            }
            catch (InvalidCommandException)
            {
                context.Respond(400, string.Format("'{0}' is not valid for the specified target", command));
            }
            catch (InputException e)
            {
                context.Respond(400, e.Message);
            }
            catch (Exception e)
            {
                context.Respond(500, e.Message);
            }
        }

        private void Launch()
        {
            var applicationPath = context.Request.QueryString["1"];
            if (string.IsNullOrEmpty(applicationPath))
                throw new ParameterMissingException("application path", 1);

            Application application = Application.Launch(applicationPath);
            int objectId = Objects.Put(application);
            context.RespondOk(objectId);
        }

        private void AttachToExistingProcess()
        {
            string parameter = context.Request.QueryString["1"];
            if (string.IsNullOrEmpty(parameter))
                throw new ParameterMissingException("process id", 1);

            int processId;
            if (!int.TryParse(parameter, out processId))
                throw new InputException("Process id should be a number");

            Application application = Application.Attach(processId);
            int objectId = Objects.Put(application);
            context.RespondOk(objectId);
        }

        private void GetWindow()
        {
            var windowTitle = context.Request.QueryString["1"];
            if (string.IsNullOrEmpty(windowTitle))
                throw new ParameterMissingException("window title", 1);

            var application = target as Application;
            if (application == null)
                throw new InvalidCommandException();

            Window window = application.GetWindow(windowTitle);
            int objectId = Objects.Put(window);
            context.RespondOk(objectId);
        }

        private void GetTitle()
        {
            var window = EnsureTargetIs<Window>();
            context.RespondOk(window.Title);            
        }

        private void IsEnabled()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            context.RespondOk(uiItem.Enabled.ToString());
        }

        private void IsOffScreen()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            context.RespondOk(uiItem.IsOffScreen.ToString());            
        }

        private void SetFocus()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            uiItem.Focus();            
        }

        private void IsFocused()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            context.RespondOk(uiItem.IsFocussed.ToString());            
        }

        private void DoubleClick()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            uiItem.DoubleClick();            
        }

        /// <summary>
        /// Enter text to the UI item.
        /// 1 - Text to enter
        /// 2 - Parent window id to wait
        /// </summary>        
        private void EnterText()
        {
            var textToEnter = context.Request.QueryString["1"];
            var windowId = context.Request.QueryString["2"];
            if (string.IsNullOrEmpty(textToEnter))
                throw new ParameterMissingException("text to enter", 1);            

            if (string.IsNullOrEmpty(windowId))
                throw new ParameterMissingException("window ref id", 2);

            if (!Objects.HasObject(Convert.ToInt32(windowId)))
                throw new InputException("Invalid reference to the window");

            var window = Objects.Get(Convert.ToInt32(windowId)) as Window;
            if (window == null)
                throw new InvalidCommandException();

            var uiItem = target as IUIItem;
            if (uiItem == null)
                throw new InvalidCommandException();

            uiItem.Enter(textToEnter);
            // This is required because uiItem.Enter() returns before it completes. We need to hold until the operation is done
            window.WaitWhileBusy();
            context.Respond(200);
        }

        private void Close()
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
                throw new InvalidCommandException();
        }

        private void GetMenubar()
        {
            var window = EnsureTargetIs<Window>();
            MenuBar menubar = window.MenuBar;
            if (menubar != null)
            {
                int menubarRefId = Objects.Put(menubar);
                context.RespondOk(menubarRefId);
            }
            else            
                context.RespondOk();                     
        }

        private void GetMenuItem()
        {
            string menuItemToFind = context.Request.QueryString["1"];
            if (String.IsNullOrEmpty(menuItemToFind))
                throw new ParameterMissingException("menu item label", 1);

            if (target is MenuBar)
            {
                var menubar = target as MenuBar;
                Menu menuItem = menubar.MenuItem(menuItemToFind);
                int menuItemId = Objects.Put(menuItem);
                context.RespondOk(menuItemId);
            }
            else if (target is Menu)
            {
                Menu menuItem = (target as Menu).SubMenu(menuItemToFind);
                int menuItemId = Objects.Put(menuItem);
                context.RespondOk(menuItemId);
            }
            else
                throw new InvalidCommandException();
        }

        private void Click()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            uiItem.Click();
            context.RespondOk();            
        }

        private void GetComboBox()
        {
            if (!(target is Window))
                throw new InvalidCommandException();

            var by = context.Request.QueryString["by"];
            if (string.IsNullOrEmpty(by))
                throw new ParameterMissingException("by");

            switch (by)
            {
                case "automationid":
                    var automationId = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(automationId))
                        throw new ParameterMissingException("automation id", 1);

                    var comboBox = (target as Window).Get<ComboBox>(SearchCriteria.ByAutomationId(automationId));
                    int comboId = Objects.Put(comboBox);
                    context.RespondOk(comboId);
                    break;
                default:
                    throw new InputException("Incorrect value for 'by'");
            }
        }

        private void SelectText()
        {
            string textToSelect = context.Request.QueryString["1"];
            if (String.IsNullOrEmpty(textToSelect))
                throw new ParameterMissingException("text to select", 1);

            var listControl = EnsureTargetIs<ListControl>();
            listControl.Select(textToSelect);
            context.RespondOk();            
        }

        private void IsEditable()
        {
            var combo = EnsureTargetIs<ComboBox>();
            context.RespondOk(combo.IsEditable.ToString());
        }
        
        private void GetSelectedItem()
        {
            if (target is ListControl)
            {
                var item = (target as ListControl).SelectedItem;
                if (item != null)
                    context.RespondOk(Objects.Put(item));
                else
                    context.RespondOk();
            }
            else if (target is ListItems)
            {
                var item = (target as ListItems).SelectedItem;
                if (item != null)
                    context.RespondOk(Objects.Put(item));
                else
                    context.RespondOk();
            }
            else
                throw new InvalidCommandException();
        }

        private void GetListItems()
        {
            var list = EnsureTargetIs<ListControl>();
            if (list.Items.Count != 0)
                context.RespondOk(Objects.Put(list.Items));
            else
                context.RespondOk();
        }

        private void GetText()
        {
            var listItem = EnsureTargetIs<ListItem>();
            context.RespondOk(listItem.Text);
        }

        private void CheckListItem()
        {
            var listItem = EnsureTargetIs<ListItem>();
            listItem.Check();
            context.RespondOk();
        }

        private void UnCheckListItem()
        {
            var listItem = EnsureTargetIs<ListItem>();
            listItem.UnCheck();
            context.RespondOk();
        }

        private void SelectListItem()
        {
            var listItem = EnsureTargetIs<ListItem>();
            listItem.Select();
            context.RespondOk();
        }

        private void IsSelected()
        {
            var listItem = EnsureTargetIs<ListItem>();
            context.RespondOk(listItem.IsSelected.ToString());
        }

        private void IsChecked()
        {
            var listItem = EnsureTargetIs<ListItem>();
            context.RespondOk(listItem.Checked.ToString());
        }

        private void GetButton()
        {
            var window = EnsureTargetIs<Window>();
            var by = context.Request.QueryString["by"];
            if (string.IsNullOrEmpty(by))
                throw new ParameterMissingException("by");

            Button button;
            switch (by)
            {
                case "automationid":
                    var automationId = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(automationId))
                        throw new ParameterMissingException("automation id", 1);

                    button = window.Get<Button>(SearchCriteria.ByAutomationId(automationId));                    
                    break;
                case "text":
                    var text = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(text))
                        throw new ParameterMissingException("text");

                    button = window.Get<Button>(SearchCriteria.ByText(text));                    
                    break;
                default:
                    throw new InputException("Incorrect value for 'by'");
            }

            int buttonId = Objects.Put(button);
            context.RespondOk(buttonId);
        }


        private bool TryGetTarget()
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

        private T EnsureTargetIs<T>()  where T : class
        {
            var result = target as T;
            if (result == null)
                throw new InvalidCommandException();

            return result;
        }
    }
}