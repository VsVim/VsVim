using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsVim
{
    internal static class Constants
    {
        internal const string ContentType = "text";


        internal static Guid VsUserData_FileNameMoniker = new Guid(0x978a8e17, 0x4df8, 0x432a, 150, 0x23, 0xd5, 0x30, 0xa2, 100, 0x52, 0xbc);

        /// <summary>
        /// Set of Key bindings which were commonly removed before key binding removal was tracked.  Used to
        /// restore key bindings on installs where the initial removal wasn't tracked
        /// </summary>
        internal static Tuple<string, Guid, string>[] CommonlyUnboundCommands = new Tuple<string, Guid, string>[] {
            Tuple.Create("Edit.LineUpExtend", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Shift+Up Arrow"),
            Tuple.Create("Edit.LineUpExtend", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Windows Forms Designer::Shift+Down Arrow"),
            Tuple.Create("Edit.LineUpExtend", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Report Designer::Shift+Up Arrow"),
            Tuple.Create("Edit.LineDownExtend", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Shift+Down Arrow"),
            Tuple.Create("Edit.LineDownExtend", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Windows Forms Designer::Shift+Up Arrow"),
            Tuple.Create("Edit.LineDownExtend", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Report Designer::Shift+Down Arrow"),
            Tuple.Create("Edit.DocumentStart", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+Home"),
            Tuple.Create("Edit.DocumentStart", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Windows Forms Designer::Home"),
            Tuple.Create("Edit.ViewTop", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+PgUp"),
            Tuple.Create("Edit.ViewBottom", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+PgDn"),
            Tuple.Create("Edit.MakeLowercase", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+U"),
            Tuple.Create("Edit.GotoBrace", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+]"),
            Tuple.Create("Edit.ViewWhiteSpace", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+R, Ctrl+W"),
            Tuple.Create("Edit.ListMembers", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Workflow Designer::Ctrl+K, L"),
            Tuple.Create("Edit.ListMembers", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Workflow Designer::Ctrl+K, Ctrl+L"),
            Tuple.Create("Edit.ListMembers", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Workflow Designer::Ctrl+J"),
            Tuple.Create("Edit.ListMembers", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+J"),
            Tuple.Create("Edit.IncrementalSearch", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Text Editor::Ctrl+I"),
            Tuple.Create("Edit.SizeControlUpGrid", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Shift+Up Arrow"),
            Tuple.Create("Edit.SizeControlDownGrid", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Shift+Down Arrow"),
            Tuple.Create("Refactor.Rename", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Ctrl+R, Ctrl+R"),
            Tuple.Create("Refactor.ExtractMethod", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Ctrl+R, Ctrl+M"),
            Tuple.Create("Refactor.EncapsulateField", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Ctrl+R, Ctrl+E"),
            Tuple.Create("Refactor.ExtractInterface", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Ctrl+R, Ctrl+I"),
            Tuple.Create("Refactor.RemoveParameters", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Ctrl+R, Ctrl+V"),
            Tuple.Create("Refactor.ReorderParameters", Guid.Parse("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}"), "Global::Ctrl+R, Ctrl+O"),
            Tuple.Create("File.Print", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+P"),
            Tuple.Create("Edit.Find", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+F"),
            Tuple.Create("File.NewFile", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+N"),
            Tuple.Create("File.OpenFile", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+O"),
            Tuple.Create("Edit.Replace", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+H"),
            Tuple.Create("Window.NextTab", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+PgDn"),
            Tuple.Create("Window.PreviousTab", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+PgUp"),
            Tuple.Create("Window.PreviousTab", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "HTML Editor Design View::Ctrl+PgUp"),
            Tuple.Create("Window.PreviousTab", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "HTML Editor Source View::Ctrl+PgUp"),
            Tuple.Create("View.NextError", Guid.Parse("{4A9B7E50-AA16-11D0-A8C5-00A0C921A4D2}"), "Global::Ctrl+Shift+F12"),
            Tuple.Create("EditorContextMenus.CodeWindow.RunSelection", Guid.Parse("{501822E1-B5AF-11D0-B4DC-00A0C91506EF}"), "Global::Ctrl+Q"),
            Tuple.Create("Debug.BreakatFunction", Guid.Parse("{C9DD4A59-47FB-11D2-83E7-00C04F9902C1}"), "Global::Ctrl+B"),
            Tuple.Create("Test.TestResults.RunCheckedTests", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, F"),
            Tuple.Create("TestResults.RunAllTestsInTestResults", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, D"),
            Tuple.Create("Test.RunTestsInClass", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, C"),
            Tuple.Create("Test.DebugTestsInClass", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, Ctrl+C"),
            Tuple.Create("Test.RunTestsInNamespace", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, N"),
            Tuple.Create("Test.DebugTestsInNamespace", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, Ctrl+N"),
            Tuple.Create("Test.RunTestsInCurrentContext", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, T"),
            Tuple.Create("Test.RunAllTestsInSolution", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, A"),
            Tuple.Create("Test.RunAllImpactedTests", Guid.Parse("{F4394F71-4DFC-4268-84C3-7D9150C5C216}"), "Global::Ctrl+R, Y"),
            Tuple.Create("Test.DebugTestsInCurrentContext", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, Ctrl+T"),
            Tuple.Create("Test.DebugAllTestsInSolution", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, Ctrl+A"),
            Tuple.Create("Test.DebugAllImpactedTests", Guid.Parse("{F4394F71-4DFC-4268-84C3-7D9150C5C216}"), "Global::Ctrl+R, Ctrl+Y"),
            Tuple.Create("TestResults.DebugCheckedTests", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, Ctrl+F"),
            Tuple.Create("TestResults.DebugAllTestsInTestResults", Guid.Parse("{B85579AA-8BE0-4C4F-A850-90902B317571}"), "Global::Ctrl+R, Ctrl+D"),
            Tuple.Create("LoadTest.JumpToCounterPane", Guid.Parse("{B032C221-57F2-4F84-8286-A33C9864379B}"), "Global::Ctrl+R, Q"),
            Tuple.Create("Edit.GoToFindCombo", Guid.Parse("{5EFC7975-14BC-11CF-9B2B-00AA00573819}"), "Global::Ctrl+D")
                    };
                }
            }
            