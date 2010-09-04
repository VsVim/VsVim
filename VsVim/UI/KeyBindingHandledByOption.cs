using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace VsVim.UI
{
    public sealed class KeyBindingHandledByOption : DependencyObject
    {
        /// <summary>
        /// Creates a new KeyBindingHandledByOption.
        /// </summary>
        /// <param name="handlerName">The name of the handler. Probably either "VsVim" or "Visual Studio".</param>
        /// <param name="handlerCommands">The list of named commands that are processed by the handler. This is
        /// processed through some heuristics to get a nice piece of text for the UI.</param>
        public KeyBindingHandledByOption(string handlerName, IEnumerable<string> handlerCommands)
        {
            this.HandlerName = handlerName;

            List<string> cleanedUpCommands = handlerCommands.ToList();
            ILookup<string, string> commandsByFirstDottedName = cleanedUpCommands.ToLookup(command => command.Split('.')[0]);

            // Do we have any large groups?
            foreach (var group in commandsByFirstDottedName)
            {
                if ((group.Key == "Test" || group.Key == "Refactor" ||
                     group.Key == "TestResults" || group.Key == "Debug") && group.Count() >= 3)
                {
                    // Let's remove those
                    cleanedUpCommands.RemoveAll(command => group.Contains(command));

                    // In it's place, we'll insert a nice shortcut
                    switch (group.Key)
                    {
                        case "Debug": cleanedUpCommands.Add("Debugging"); break;
                        case "Refactor": cleanedUpCommands.Add("Refactoring"); break;
                        case "Test": cleanedUpCommands.Add("Testing"); break;
                        case "TestResults": cleanedUpCommands.Add("Test Results"); break;
                    }
                }
                else if (group.Key == "Edit")
                {
                    // Do we have a bunch of outlining commands here?
                    var outliningCommands = group.Where(command => command.Contains("Outlining") ||
                                                                   command.Contains("Region") ||
                                                                   command.Contains("Collapse")).ToList();
                    if (outliningCommands.Count >= 3)
                    {
                        cleanedUpCommands.RemoveAll(command => outliningCommands.Contains(command));
                        cleanedUpCommands.Add("Outlining");
                    }

                    // And a bunch of bookmark commands?
                    var bookmarkCommands = group.Where(command => command.Contains("Bookmark")).ToList();
                    if (bookmarkCommands.Count >= 3)
                    {
                        cleanedUpCommands.RemoveAll(command => bookmarkCommands.Contains(command));
                        cleanedUpCommands.Add("Bookmarks");
                    }
                }
            }

            if (cleanedUpCommands.Count != 0)
            {
                cleanedUpCommands.Sort();
                HandlerDetails = "Used by " + string.Join(", ", cleanedUpCommands);
            }
        }

        public string HandlerName { get; private set; }
        public string HandlerDetails { get; private set; }
    }
}
