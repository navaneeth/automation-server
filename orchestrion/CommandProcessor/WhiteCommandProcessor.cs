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
using White.Core.UIItems.TreeItems;
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
                    {"getwindowfromrefid", GetWindowFromRefId},
                    {"getmodalwindow", GetModalWindow},
                    {"getmodalwindows", GetModalWindows},

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
                    {"waitwhilebusy", WaitWhileBusy},
                    
                    {"gethscrollbar", GetHorizontalScrollBar},
                    {"getvscrollbar", GetVerticalScrollBar},
                    {"scrollleft", ScrollLeft},
                    {"scrollright", ScrollRight},
                    {"scrollup", ScrollUp},
                    {"scrolldown", ScrollDown},

                    {"getmenuitem", GetMenuItem},
                    {"enter", EnterText},
                    {"click", Click},
                    {"rightclick", RightClick},
                    {"toggle", Toggle},
                    
                    {"getcombobox", GetComboBox},
                    {"selecttext", SelectText},
                    {"iseditable", IsEditable},
                    {"getselecteditem", GetSelectedItem},
                    {"getlistitems", GetListItems},
                    {"getitembyindex", GetItemByIndex},
                    {"getitembytext", GetItemByText},
                    {"getitemscount", GetItemsCount},
                    {"getchildren", GetChildren},
                    {"getnodes", GetNodes},
                    {"getnode", GetNode},
                    {"getselectednode", GetSelectedNode},

                    {"getlistbox", GetListBox},
                    {"gettextbox", GetTextBox},
                    {"getlabel", GetLabel},
                    {"gettree", GetTree},
                    {"getmultilinetextbox", GetMultiLineTextBox},
                    {"getmessagebox", GetMessageBox},
                    {"getprogressbar", GetProgressBar},
                    {"getcheckbox", GetCheckBox},
                    {"getradiobutton", GetRadioButton},
                    {"getslider", GetSlider},
                    {"gethyperlink", GetHyperlink},
                    
                    {"gettext", GetText},
                    {"getvalue", GetValue},
                    {"settext", SetText},
                    {"isreadonly", IsReadonly},
                    {"check", Check},
                    {"uncheck", UnCheck},
                    {"select", Select},
                    {"isselected", IsSelected},
                    {"ischecked", IsChecked},
                    {"increment", Increment},
                    {"decrement", Decrement},

                    {"expand", Expand},
                    {"collapse", Collapse},
                    {"isexpanded", IsExpanded},
                    {"selecttreenode", SelectTreeNode},
                    {"deselecttreenode", DeselectTreeNode},
  
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
            var application = EnsureTargetIs<Application>();
            var windowTitle = GetParameter(1, "title");
            Window window = application.GetWindow(windowTitle);            
            context.RespondOk(Objects.Put(window));
        }

        private void GetWindowFromRefId()
        {
            var window = EnsureTargetIs<Window>();
            context.RespondOk(Objects.Put(window));
        }

        private void GetModalWindow()
        {
            var window = EnsureTargetIs<Window>();
            var modalWindow = window.ModalWindow(GetSearchCriteria());            
            context.RespondOk(Objects.Put(modalWindow));
        }

        private void GetModalWindows()
        {
            var window = EnsureTargetIs<Window>();
            var windows = window.ModalWindows();
            if (windows != null && windows.Count > 0)
                context.RespondOk(Objects.Put(windows));
            else
                context.RespondOk();
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

        private void GetTree()
        {
            var window = EnsureTargetIs<Window>();
            var tree = window.Get<Tree>(GetSearchCriteria());
            context.RespondOk(Objects.Put(tree));
        }

        private void WaitWhileBusy()
        {
            if (target is Application)
            {
                (target as Application).WaitWhileBusy();
                context.RespondOk();
            }
            else if (target is Window)
            {
                (target as Window).WaitWhileBusy();
                context.RespondOk();
            }
            else
                throw new InvalidCommandException();
        }

        private void GetMaxValue()
        {
            if (target is IScrollBar)
            {
                context.RespondOk((target as IScrollBar).MaximumValue.ToString(CultureInfo.InvariantCulture));
            }
            else if (target is ProgressBar)
            {
                context.RespondOk((target as ProgressBar).Maximum.ToString(CultureInfo.InvariantCulture));
            }
            else
                throw new InvalidCommandException();            
        }

        private void GetMinValue()
        {
            if (target is IScrollBar)
            {
                context.RespondOk((target as IScrollBar).MinimumValue.ToString(CultureInfo.InvariantCulture));
            }
            else if (target is ProgressBar)
            {
                context.RespondOk((target as ProgressBar).Minimum.ToString(CultureInfo.InvariantCulture));
            }
            else
                throw new InvalidCommandException();
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
        
        private void EnterText()
        {
            var uiItem = EnsureTargetIs<IUIItem>();
            var textToEnter = GetParameter(1, "text");            
            uiItem.Enter(textToEnter);
            context.RespondOk();
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

        private void GetLabel()
        {
            var window = EnsureTargetIs<Window>();
            var label = window.Get<Label>(GetSearchCriteria());
            context.RespondOk(Objects.Put(label));
        }

        public void GetMultiLineTextBox()
        {
            var window = EnsureTargetIs<Window>();
            var textBox = window.Get<MultilineTextBox>(GetSearchCriteria());
            context.RespondOk(Objects.Put(textBox));
        }

        private void GetMessageBox()
        {
            var window = EnsureTargetIs<Window>();
            var title = GetParameter(1, "title");
            Window messageBox = window.MessageBox(title);
            if (messageBox == null)
                throw new InvalidOperationException("Error finding message box with title '" + title + "'");
            
            context.RespondOk(Objects.Put(messageBox));            
        }

        private void GetProgressBar()
        {
            var window = EnsureTargetIs<Window>();
            var progressBar = window.Get<ProgressBar>(GetSearchCriteria());            
            context.RespondOk(Objects.Put(progressBar));
        }

        private void GetCheckBox()
        {
            var window = EnsureTargetIs<Window>();
            var checkBox = window.Get<CheckBox>(GetSearchCriteria());
            context.RespondOk(Objects.Put(checkBox));
        }

        private void GetRadioButton()
        {
            var window = EnsureTargetIs<Window>();
            var radioButton = window.Get<RadioButton>(GetSearchCriteria());
            context.RespondOk(Objects.Put(radioButton));
        }

        private void GetSlider()
        {
            var window = EnsureTargetIs<Window>();
            var slider = window.Get<Slider>(GetSearchCriteria());
            context.RespondOk(Objects.Put(slider));
        }

        private void GetHyperlink()
        {
            var window = EnsureTargetIs<Window>();
            var link = window.Get<Hyperlink>(GetSearchCriteria());
            context.RespondOk(Objects.Put(link));
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

        private void GetItemByIndex()
        {
            int index;
            if (!int.TryParse(GetParameter(1, "index"), out index))
                throw new InputException("Incorrect value for index");

            if (index < 0)
                throw new InputException("Invalid index");

            if (target is ListItems)
            {
                var listItems = target as ListItems;
                context.RespondOk(Objects.Put(listItems.Item(index)));
            }
            else if (target is Menus)
            {
                var menuItems = target as Menus;
                context.RespondOk(Objects.Put(menuItems[index]));
            }
            else if (target is TreeNodes)
            {
                var treeNodes = target as TreeNodes;
                context.RespondOk(Objects.Put(treeNodes[index]));
            }
            else if (target is List<Window>)
            {
                context.RespondOk(Objects.Put((target as List<Window>)[index]));
            }
            else
                throw new InvalidCommandException();
        }

        private void GetItemByText()
        {
            var listItems = EnsureTargetIs<ListItems>();
            var text = GetParameter(1, "text");
            context.RespondOk(Objects.Put(listItems.Item(text)));
        }

        private void GetItemsCount()
        {
            if (target is ListItems)
            {
                context.RespondOk((target as ListItems).Count);
            }
            else if (target is Menus)
            {
                context.RespondOk((target as Menus).Count);
            }
            else if (target is TreeNodes)
            {
                context.RespondOk((target as TreeNodes).Count);
            }
            else if (target is List<Window>)
            {
                context.RespondOk((target as List<Window>).Count);
            }
            else
                throw new InvalidCommandException();
        }

        private void GetChildren()
        {
            if (target is Menu)
            {
                var menuItem = target as Menu;
                if (menuItem.ChildMenus != null && menuItem.ChildMenus.Count > 0)
                    context.RespondOk(Objects.Put(menuItem.ChildMenus));
                else
                    context.RespondOk();
            }
            else if (target is TreeNode)
            {
                var treeNode = target as TreeNode;
                if (treeNode.Nodes != null && treeNode.Nodes.Count > 0)
                    context.RespondOk(Objects.Put(treeNode.Nodes));
                else
                    context.RespondOk();
            }
            else
                throw new InvalidCommandException();
        }

        private void GetNodes()
        {
            var tree = EnsureTargetIs<Tree>();
            if (tree.Nodes != null && tree.Nodes.Count > 0)
                context.RespondOk(Objects.Put(tree.Nodes));
            else
                context.RespondOk();
        }

        private void GetNode()
        {
            var nodeText = GetParameter(1, "node text");

            if (target is Tree)
            {
                var node = (target as Tree).Node(nodeText);
                context.RespondOk(Objects.Put(node));
            }
            else if (target is TreeNode)
            {
                var node = (target as TreeNode).GetItem(nodeText);
                context.RespondOk(Objects.Put(node));
            }
        }

        private void GetSelectedNode()
        {
            var tree = EnsureTargetIs<Tree>();
            if (tree.SelectedNode != null)
                context.RespondOk(Objects.Put(tree.SelectedNode));
            else
                context.RespondOk();            
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
            else if (target is Label)
            {
                context.RespondOk((target as Label).Text);
            }
            else if (target is TreeNode)
            {
                context.RespondOk((target as TreeNode).Text);
            }
            else
                throw new InvalidCommandException();
        }

        private void GetValue()
        {
            if (target is ProgressBar)
                context.RespondOk((target as ProgressBar).Value.ToString(CultureInfo.InvariantCulture));
            else if (target is IScrollBar)
                context.RespondOk((target as IScrollBar).Value.ToString(CultureInfo.InvariantCulture));
            else if (target is Slider)
                context.RespondOk((target as Slider).Value.ToString(CultureInfo.InvariantCulture));
            else
                throw new InvalidCommandException();
        }

        private void SetText()
        {
            var textBox = EnsureTargetIs<TextBox>();
            var textToSet = GetParameter(1, "text");

            textBox.Text = textToSet;
            context.RespondOk();
        }

        private void IsReadonly()
        {
            var textBox = EnsureTargetIs<TextBox>();
            context.RespondOk(textBox.IsReadOnly.ToString());
        }

        private void Check()
        {
            if (target is ListItem)
            {
                (target as ListItem).Check();
                context.RespondOk();
            }
            else if (target is CheckBox)
            {
                (target as CheckBox).Checked = true;
                context.RespondOk();
            }
            else
                throw new InvalidCommandException();
        }

        private void UnCheck()
        {
            if (target is ListItem)
            {
                (target as ListItem).UnCheck();
                context.RespondOk();
            }
            else if (target is CheckBox)
            {
                (target as CheckBox).Checked = false;
                context.RespondOk();
            }
            else
                throw new InvalidCommandException();
        }

        private void Select()
        {
            if (target is ListItem)
            {
                (target as ListItem).Select();
                context.RespondOk();
            }
            else if (target is RadioButton)
            {
                (target as RadioButton).Select();
                context.RespondOk();
            }
            else
                throw new InvalidCommandException();            
        }

        private void IsSelected()
        {
            if (target is ListItem)
                context.RespondOk((target as ListItem).IsSelected.ToString());
            else if (target is TreeNode)
                context.RespondOk((target as TreeNode).IsSelected.ToString());
            else if (target is RadioButton)
                context.RespondOk((target as RadioButton).IsSelected.ToString());
            else
                throw new InvalidCommandException();
        }

        private void IsChecked()
        {
            if (target is ListItem)
                context.RespondOk((target as ListItem).Checked.ToString());
            else if (target is CheckBox)
                context.RespondOk((target as CheckBox).Checked.ToString());
            else
                throw new InvalidCommandException();
        }

        private void Increment()
        {
            var slider = EnsureTargetIs<Slider>();
            slider.SmallIncrement();
            context.RespondOk();
        }

        private void Decrement()
        {
            var slider = EnsureTargetIs<Slider>();
            slider.SmallDecrement();
            context.RespondOk();
        }

        private void Expand()
        {
            var node = EnsureTargetIs<TreeNode>();
            node.Expand();
            context.RespondOk();
        }

        private void Collapse()
        {
            var node = EnsureTargetIs<TreeNode>();
            node.Collapse();
            context.RespondOk();
        }

        private void IsExpanded()
        {
            var node = EnsureTargetIs<TreeNode>();            
            context.RespondOk(node.IsExpanded().ToString());
        }

        private void SelectTreeNode()
        {
            var node = EnsureTargetIs<TreeNode>();
            node.Select();
            context.RespondOk();
        }

        private void DeselectTreeNode()
        {
            var node = EnsureTargetIs<TreeNode>();
            node.UnSelect();
            context.RespondOk();
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

        private bool IsParameterAvailable(int param)
        {
            string result = context.Request.QueryString[param.ToString(CultureInfo.InvariantCulture)];
            if (string.IsNullOrEmpty(result))
                return false;

            return true;
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