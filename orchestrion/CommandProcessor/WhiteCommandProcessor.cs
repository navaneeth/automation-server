using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Orchestrion.Core;
using Orchestrion.Extensions;
using White.Core;
using White.Core.UIItems;
using White.Core.UIItems.Finders;
using White.Core.UIItems.ListBoxItems;
using White.Core.UIItems.MenuItems;
using White.Core.UIItems.Scrolling;
using White.Core.UIItems.WindowItems;
using White.Core.UIItems.WindowStripControls;

namespace Orchestrion.CommandProcessor
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
                    {"isvisible", IsVisible},
                    {"getname", GetName},
                    {"canscroll", CanScroll},
                    {"getminvalue", GetMinValue},
                    {"getmaxvalue", GetMaxValue},
                    
                    {"gethscrollbar", GetHorizontalScrollBar},
                    {"getvscrollbar", GetVerticalScrollBar},
                    {"scrollleft", ScrollLeft},
                    {"scrollright", ScrollRight},
                    {"scrollup", ScrollUp},
                    {"scrolldown", ScrollDown},

                    {"getmenuitem", GetMenuItem},
                    {"entertext", EnterText},
                    {"click", Click},
                    {"rightclick", RightClick},
                    {"toggle", Toggle},
                    
                    {"getcombobox", GetComboBox},
                    {"selecttext", SelectText},
                    {"iseditable", IsEditable},
                    {"getselecteditem", GetSelectedItem},
                    {"getlistitems", GetListItems},
                    {"getlistitembyindex", GetListItemByIndex},
                    {"getlistitembytext", GetListItemByText},
                    {"getlistitemscount", GetListItemsCount},

                    {"getlistbox", GetListBox},
                    {"gettextbox", GetTextBox},
                    {"getmultilinetextbox", GetMultiLineTextBox},
                    
                    {"gettext", GetText},
                    {"settext", SetText},
                    {"isreadonly", IsReadonly},
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

        private void IsVisible()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            context.RespondOk(uiItem.Visible.ToString());
        }

        private void GetName()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            context.RespondOk(uiItem.Name);
        }

        private void CanScroll()
        {
            if (target is IUIItem)
            {
                var scrollbars = (target as IUIItem).ScrollBars;
                context.RespondOk(scrollbars == null ? "false" : scrollbars.CanScroll.ToString());
            }
            else if (target is IHScrollBar)
            {
                context.RespondOk((target as IHScrollBar).IsScrollable.ToString());
            }
            else if (target is IVScrollBar)
            {
                context.RespondOk((target as IVScrollBar).IsScrollable.ToString());
            }
            else
                throw new InvalidCommandException();
        }

        private void GetMaxValue()
        {
            var scrollbar = EnsureTargetIs<IScrollBar>();
            context.RespondOk(scrollbar.MaximumValue.ToString(CultureInfo.InvariantCulture));
        }

        private void GetMinValue()
        {
            var scrollbar = EnsureTargetIs<IScrollBar>();
            context.RespondOk(scrollbar.MinimumValue.ToString(CultureInfo.InvariantCulture));
        }

        private void GetHorizontalScrollBar()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            if (uiItem.ScrollBars != null && uiItem.ScrollBars.CanScroll)
                context.RespondOk(Objects.Put(uiItem.ScrollBars.Horizontal));
            else
                context.RespondOk();
        }

        private void GetVerticalScrollBar()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            if (uiItem.ScrollBars != null && uiItem.ScrollBars.CanScroll)
                context.RespondOk(Objects.Put(uiItem.ScrollBars.Vertical));
            else
                context.RespondOk();
        }

        private void ScrollLeft()
        {
            var hscroll = EnsureTargetIs<IHScrollBar>();
            hscroll.ScrollLeft();
            context.RespondOk();
        }

        private void ScrollRight()
        {
            var hscroll = EnsureTargetIs<IHScrollBar>();
            hscroll.ScrollRight();
            context.RespondOk();
        }

        private void ScrollUp()
        {
            var hscroll = EnsureTargetIs<IVScrollBar>();
            hscroll.ScrollUp();
            context.RespondOk();
        }

        private void ScrollDown()
        {
            var hscroll = EnsureTargetIs<IVScrollBar>();
            hscroll.ScrollDown();
            context.RespondOk();
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
                context.RespondOk();
            }
            else if (target is Window)
            {
                var window = target as Window;
                window.Close();
                window.Dispose();
                Objects.Remove(currentRefId);
                context.RespondOk();
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

        private void RightClick()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            uiItem.RightClick();
            context.RespondOk();
        }

        private void GetComboBox()
        {
            var window = EnsureTargetIs<Window>();
            var comboBox = window.Get<ComboBox>(GetSearchCriteria());
            context.RespondOk(Objects.Put(comboBox));
        }        

        private void GetListBox()
        {
            var window = EnsureTargetIs<Window>();
            var listBox = window.Get<ListBox>(GetSearchCriteria());
            context.RespondOk(Objects.Put(listBox));
        }

        private void GetTextBox()
        {
            var window = EnsureTargetIs<Window>();
            var textBox = window.Get<TextBox>(GetSearchCriteria());
            context.RespondOk(Objects.Put(textBox));            
        }

        public void GetMultiLineTextBox()
        {
            var window = EnsureTargetIs<Window>();
            var textBox = window.Get<MultilineTextBox>(GetSearchCriteria());
            context.RespondOk(Objects.Put(textBox));
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

        private void GetListItemByIndex()
        {
            var listItems = EnsureTargetIs<ListItems>();
            
            int index;
            if (!int.TryParse(GetParameter(1, "index"), out index))
                throw new InputException("Incorrect value for index");

            if (index < 0)
                throw new InputException("Invalid index");

            context.RespondOk(Objects.Put(listItems.Item(index)));
        }

        private void GetListItemByText()
        {
            var listItems = EnsureTargetIs<ListItems>();
            var text = GetParameter(1, "text");
            context.RespondOk(Objects.Put(listItems.Item(text)));
        }

        private void GetListItemsCount()
        {
            var listItems = EnsureTargetIs<ListItems>();
            context.RespondOk(listItems.Count);
        }

        private void Toggle()
        {
            var button = EnsureTargetIs<Button>();
            button.Toggle();
            context.RespondOk();
        }
        
        private void GetText()
        {
            if (target is ListItem)
            {
                context.RespondOk((target as ListItem).Text);
            }
            else if (target is TextBox)
            {
                context.RespondOk((target as TextBox).Text);
            }
            else
                throw new InvalidCommandException();
        }

        private void SetText()
        {
            var textBox = EnsureTargetIs<TextBox>();
            var textToSet = GetParameter(1, "text");

            // BulkText seems to be more efficient in setting
            textBox.BulkText = textToSet;
            context.RespondOk();
        }

        private void IsReadonly()
        {
            var textBox = EnsureTargetIs<TextBox>();
            context.RespondOk(textBox.IsReadOnly.ToString());
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
            var button = window.Get<Button>(GetSearchCriteria());            
            context.RespondOk(Objects.Put(button));
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

        private string GetParameter(int param, string parameterName)
        {
            string result = context.Request.QueryString[param.ToString(CultureInfo.InvariantCulture)];
            if (string.IsNullOrEmpty(result))
                throw new ParameterMissingException(parameterName, param);
            
            return result;
        }

        private SearchCriteria GetSearchCriteria()
        {
            var by = context.Request.QueryString["by"];
            if (string.IsNullOrEmpty(by))
                throw new ParameterMissingException("by");

            switch (by)
            {
                case "automationid":
                    var automationId = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(automationId))
                        throw new ParameterMissingException("automation id", 1);

                    return SearchCriteria.ByAutomationId(automationId);
                case "text":
                    var text = context.Request.QueryString["1"];
                    if (string.IsNullOrEmpty(text))
                        throw new ParameterMissingException("text");

                    return SearchCriteria.ByText(text);
                default:
                    throw new InputException("Incorrect value for 'by'");
            }
        }
    }
}