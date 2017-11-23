﻿using System;
using System.Linq;
using Vim;
using Vim.UnitTest;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public sealed class KeyBindingTest
    {
        #region Command Resource

        internal const string SampleCommandString = @"
Global::Shift+Alt+N
Global::Shift+Alt+O
Managed Resources Editor::Ctrl+Del
Settings Designer::Ctrl+Del
Managed Resources Editor::F2
Settings Designer::F2
ADO.NET Entity Data Model Designer::Ctrl+2
ADO.NET Entity Data Model Designer::Ctrl+1
Global::Ctrl+Alt+F
Text Editor::Alt+Enter
Text Editor::Alt+'
Global::Ctrl+\, Ctrl+M
Global::Ctrl+\, Ctrl+R
Global::Ctrl+\, Ctrl+U
Text Editor::Bkspce
Text Editor::Shift+Bkspce
Text Editor::Enter
Text Editor::Shift+Enter
Windows Forms Designer::Enter
Report Designer::Enter
Text Editor::Tab
Windows Forms Designer::Tab
Report Designer::Tab
Text Editor::Shift+Tab
Windows Forms Designer::Shift+Tab
Report Designer::Shift+Tab
Text Editor::Left Arrow
Windows Forms Designer::Left Arrow
Report Designer::Left Arrow
Text Editor::Shift+Left Arrow
Windows Forms Designer::Shift+Left Arrow
Report Designer::Shift+Left Arrow
Text Editor::Right Arrow
Windows Forms Designer::Right Arrow
Report Designer::Right Arrow
Text Editor::Shift+Right Arrow
Windows Forms Designer::Shift+Right Arrow
Report Designer::Shift+Right Arrow
Text Editor::Up Arrow
Windows Forms Designer::Up Arrow
Report Designer::Up Arrow
Text Editor::Shift+Up Arrow
Windows Forms Designer::Shift+Down Arrow
Report Designer::Shift+Up Arrow
Text Editor::Down Arrow
Windows Forms Designer::Down Arrow
Report Designer::Down Arrow
Text Editor::Shift+Down Arrow
Windows Forms Designer::Shift+Up Arrow
Report Designer::Shift+Down Arrow
Text Editor::Ctrl+Home
Windows Forms Designer::Home
Text Editor::Ctrl+Shift+Home
Windows Forms Designer::Shift+Home
Text Editor::Ctrl+End
Windows Forms Designer::End
Text Editor::Ctrl+Shift+End
Windows Forms Designer::Shift+End
Text Editor::Home
Text Editor::Shift+Home
Text Editor::End
Text Editor::Shift+End
Text Editor::PgUp
Text Editor::Shift+PgUp
Text Editor::PgDn
Text Editor::Shift+PgDn
Text Editor::Ctrl+PgUp
Text Editor::Ctrl+Shift+PgUp
Text Editor::Ctrl+PgDn
Text Editor::Ctrl+Shift+PgDn
Text Editor::Ctrl+Up Arrow
VC Dialog Editor::Ctrl+Up Arrow
Text Editor::Ctrl+Down Arrow
VC Dialog Editor::Ctrl+Down Arrow
VC Dialog Editor::Ctrl+Left Arrow
VC Dialog Editor::Ctrl+Right Arrow
Text Editor::Ctrl+U
Text Editor::Ctrl+Shift+U
Text Editor::Ctrl+K, Ctrl+A
Text Editor::Ctrl+]
Text Editor::Ctrl+Shift+]
Text Editor::Ins
Text Editor::Ctrl+L
Text Editor::Ctrl+Shift+L
Text Editor::Ctrl+K, Ctrl+\
Text Editor::Ctrl+Enter
Text Editor::Ctrl+Shift+Enter
Text Editor::Ctrl+K, Ctrl+L
Text Editor::Ctrl+K, Ctrl+K
Global::Ctrl+K, Ctrl+N
Global::Ctrl+K, Ctrl+P
Text Editor::Ctrl+T
Text Editor::Ctrl+Shift+T
Text Editor::Shift+Alt+T
Text Editor::Ctrl+W
Text Editor::Ctrl+Del
Text Editor::Ctrl+Bkspce
Text Editor::Ctrl+Left Arrow
Text Editor::Ctrl+Shift+Left Arrow
Text Editor::Ctrl+Right Arrow
Text Editor::Ctrl+Shift+Right Arrow
Text Editor::Esc
Windows Forms Designer::Esc
Report Designer::Esc
Managed Resources Editor::Esc
Settings Designer::Esc
Workflow Designer::Ctrl+K, Ctrl+P
Workflow Designer::Ctrl+K, P
Workflow Designer::Ctrl+Shift+Space
Text Editor::Ctrl+Shift+Space
Text Editor::Ctrl+R, Ctrl+W
Workflow Designer::Ctrl+K, Ctrl+W
Workflow Designer::Ctrl+K, W
Workflow Designer::Ctrl+Space
Workflow Designer::Alt+Right Arrow
Text Editor::Ctrl+Space
Text Editor::Alt+Right Arrow
Workflow Designer::Ctrl+K, L
Workflow Designer::Ctrl+K, Ctrl+L
Workflow Designer::Ctrl+J
Text Editor::Ctrl+J
Text Editor::Ctrl+K, Ctrl+F
Text Editor::Ctrl+K, Ctrl+H
Workflow Designer::Ctrl+K, I
Workflow Designer::Ctrl+K, Ctrl+I
Text Editor::Ctrl+K, Ctrl+I
Text Editor::Shift+Alt+Left Arrow
Text Editor::Shift+Alt+Right Arrow
Text Editor::Shift+Alt+Up Arrow
Text Editor::Shift+Alt+Down Arrow
Text Editor::Ctrl+E, Ctrl+W
Text Editor::Ctrl+I
Text Editor::Ctrl+Shift+I
Text Editor::Shift+Alt+Home
Text Editor::Shift+Alt+End
Text Editor::Ctrl+Shift+Alt+Left Arrow
Text Editor::Ctrl+Shift+Alt+Right Arrow
Text Editor::Ctrl+M, Ctrl+H
Text Editor::Ctrl+M, Ctrl+M
Text Editor::Ctrl+M, Ctrl+L
Text Editor::Ctrl+M, Ctrl+P
Text Editor::Ctrl+M, Ctrl+U
Text Editor::Ctrl+M, Ctrl+O
Text Editor::Ctrl+K, Ctrl+C
Text Editor::Ctrl+K, Ctrl+U
Global::Ctrl+Shift+G
Text Editor::Ctrl+=
Text Editor::Ctrl+K, Ctrl+D
Workflow Designer::Alt+.
Text Editor::Alt+.
Workflow Designer::Alt+,
Text Editor::Alt+,
Global::Shift+Alt+F10
HTML Editor Design View::Shift+Alt+F10
Global::Ctrl+.
Text Editor::Ctrl+Shift+Alt+C
Text Editor::Ctrl+Shift+Alt+P
HTML Editor Design View::Ctrl+Shift+L
HTML Editor Design View::Ctrl+L
HTML Editor Source View::Ctrl+Alt+.
HTML Editor Design View::Ctrl+M, Ctrl+M
Global::Ctrl+K, Ctrl+X
Text Editor::Ctrl+M, Ctrl+T
Global::Ctrl+F7
HTML Editor Design View::Ctrl+M, Ctrl+C
HTML Editor Design View::Shift+F7
Global::Ctrl+K, Ctrl+F
Global::Ctrl+Shift+K, Ctrl+Shift+N
Global::Ctrl+Shift+K, Ctrl+Shift+P
VC Dialog Editor::Ctrl+T
VC Dialog Editor::Alt+Right Arrow
VC Dialog Editor::Alt+Left Arrow
VC Dialog Editor::Alt+Up Arrow
VC Dialog Editor::Alt+Down Arrow
VC Dialog Editor::Ctrl+G
VC Dialog Editor::Shift+F7
VC Dialog Editor::Ctrl+F9
VC Dialog Editor::Ctrl+Shift+F9
VC Dialog Editor::Ctrl+D
VC Dialog Editor::Ctrl+R
VC Dialog Editor::Ctrl+B
Global::Ctrl+Left Arrow
Windows Forms Designer::Ctrl+Left Arrow
Report Designer::Ctrl+Left Arrow
VC Dialog Editor::Left Arrow
Global::Ctrl+Down Arrow
HTML Editor Design View::Ctrl+Down Arrow
Windows Forms Designer::Ctrl+Down Arrow
Report Designer::Ctrl+Down Arrow
VC Dialog Editor::Down Arrow
Global::Ctrl+Right Arrow
Windows Forms Designer::Ctrl+Right Arrow
Report Designer::Ctrl+Right Arrow
VC Dialog Editor::Right Arrow
Global::Ctrl+Up Arrow
HTML Editor Design View::Ctrl+Up Arrow
Windows Forms Designer::Ctrl+Up Arrow
Report Designer::Ctrl+Up Arrow
VC Dialog Editor::Up Arrow
Global::Ctrl+Shift+Down Arrow
Windows Forms Designer::Ctrl+Shift+Down Arrow
Report Designer::Ctrl+Shift+Down Arrow
VC Dialog Editor::Shift+Down Arrow
Global::Ctrl+Shift+Up Arrow
Windows Forms Designer::Ctrl+Shift+Up Arrow
Report Designer::Ctrl+Shift+Up Arrow
VC Dialog Editor::Shift+Up Arrow
Global::Ctrl+Shift+Left Arrow
Windows Forms Designer::Ctrl+Shift+Left Arrow
Report Designer::Ctrl+Shift+Left Arrow
VC Dialog Editor::Shift+Left Arrow
Global::Ctrl+Shift+Right Arrow
Windows Forms Designer::Ctrl+Shift+Right Arrow
Report Designer::Ctrl+Shift+Right Arrow
VC Dialog Editor::Shift+Right Arrow
VC Accelerator Editor::Ins
VC Accelerator Editor::Ctrl+W
VC Image Editor::Ctrl+H
VC Image Editor::Shift+Alt+H
VC Image Editor::Ctrl+Shift+H
VC String Editor::Ins
VC Dialog Editor::Ctrl+M
VC Image Editor::Ctrl+J
VC Image Editor::Ins
VC Image Editor::Ctrl+Alt+S
VC Image Editor::Ctrl+Shift+Alt+S
VC Image Editor::Ctrl+Shift+M
VC Image Editor::Shift+Alt+S
VC Image Editor::Ctrl+Shift+I
VC Image Editor::Ctrl+F
VC Image Editor::Ctrl+I
VC Image Editor::Ctrl+B
VC Image Editor::Ctrl+A
VC Image Editor::Ctrl+L
VC Image Editor::Ctrl+T
VC Image Editor::Alt+R
VC Image Editor::Shift+Alt+R
VC Image Editor::Ctrl+Shift+Alt+R
VC Image Editor::Alt+W
VC Image Editor::Shift+Alt+W
VC Image Editor::Ctrl+Shift+Alt+W
VC Image Editor::Alt+P
VC Image Editor::Shift+Alt+P
VC Image Editor::Ctrl+Shift+Alt+P
VC Image Editor::Ctrl+M
VC Image Editor::Ctrl+=
VC Image Editor::Ctrl+.
VC Image Editor::Ctrl+-
VC Image Editor::Ctrl+Shift+.
VC Image Editor::Ctrl+Up Arrow
VC Image Editor::Ctrl+Shift+,
VC Image Editor::Ctrl+Down Arrow
VC Image Editor::Ctrl+[
VC Image Editor::Ctrl+Left Arrow
VC Image Editor::Ctrl+Shift+[
VC Image Editor::Ctrl+Shift+Left Arrow
VC Image Editor::Ctrl+]
VC Image Editor::Ctrl+Right Arrow
VC Image Editor::Ctrl+Shift+]
VC Image Editor::Ctrl+Shift+Right Arrow
Global::Enter
Global::Up Arrow
Global::Down Arrow
Global::Left Arrow
Global::Right Arrow
Global::Shift+Right Arrow
Global::Shift+Up Arrow
Global::Shift+Left Arrow
Global::Shift+Down Arrow
Global::Tab
Global::Shift+Tab
Global::Ctrl+R, Ctrl+R
Global::Ctrl+R, Ctrl+M
Global::Ctrl+R, Ctrl+E
Global::Ctrl+R, Ctrl+I
Global::Ctrl+R, Ctrl+V
Global::Ctrl+R, Ctrl+O
Global::Ctrl+K, Ctrl+M
Global::Ctrl+K, Ctrl+S
Global::Ctrl+Alt+Down Arrow
Global::Alt+F7
Global::Shift+Alt+F7
Global::Ctrl+K, Ctrl+V
Global::Ctrl+K, Ctrl+R
Global::Alt+Left Arrow
Global::Alt+Right Arrow
Global::Ctrl+\, D
Global::Ctrl+\, Ctrl+D
Global::Ctrl+Alt+S
Global::Ctrl+K, Ctrl+T
Global::Ctrl+K, T
Text Editor::Ctrl+Alt+Space
Text Editor::Ctrl+Shift+Down Arrow
Text Editor::Ctrl+Shift+Up Arrow
VC Dialog Editor::Ctrl+Shift+Down Arrow
VC Dialog Editor::F9
VC Dialog Editor::Ctrl+Shift+Left Arrow
VC Dialog Editor::Ctrl+Shift+Right Arrow
VC Dialog Editor::Ctrl+Shift+Up Arrow
VC Dialog Editor::Shift+F9
Global::Ctrl+C
Global::Ctrl+Ins
Global::Ctrl+X
Global::Shift+Del
Global::Del
Sequence Diagram::Shift+Del
UML Activity Diagram::Shift+Del
Layer Diagram::Shift+Del
Class Diagram::Ctrl+Del
Global::Ctrl+V
Global::Shift+Ins
Global::Ctrl+P
Global::Ctrl+Y
Global::Ctrl+Shift+Z
Global::Shift+Alt+Bkspce
Global::Ctrl+A
Global::Ctrl+Alt+X
Global::Ctrl+Z
Global::Alt+Bkspce
HTML Editor Design View::Ctrl+B
HTML Editor Design View::Ctrl+I
HTML Editor Design View::Ctrl+U
Global::Ctrl+F
Query Designer::Ctrl+3
View Designer::Ctrl+3
Query Designer::Ctrl+1
View Designer::Ctrl+1
Query Designer::Ctrl+4
View Designer::Ctrl+4
Query Designer::Ctrl+2
View Designer::Ctrl+2
Managed Resources Editor::Del
Global::Shift+F5
Global::Ctrl+Alt+Break
Team Foundation Build Detail Editor::F5
Query Designer::Ctrl+R
View Designer::Ctrl+R
Query Designer::Ctrl+G
View Designer::Ctrl+G
Global::Ctrl+Alt+H
Global::Ctrl+Shift+N
Global::Ctrl+Shift+O
Global::Ctrl+Shift+A
Global::Ctrl+N
Global::Ctrl+O
Global::Ctrl+Shift+S
Global::Alt+F4
Global::Ctrl+H
Global::Ctrl+G
Global::Shift+F4
Global::Shift+Alt+Enter
Global::Ctrl+Alt+L
Global::F4
Global::Ctrl+\, T
Global::Ctrl+\, Ctrl+T
Global::Ctrl+Alt+O
Global::Ctrl+Alt+J
Global::Ctrl+Alt+T
Global::Ctrl+Alt+I
Global::Ctrl+Alt+V, L
Global::Ctrl+Alt+C
Global::Shift+Alt+A
Global::F11
Global::F10
Global::Shift+F11
Global::Ctrl+F10
Global::Shift+F9
Global::Ctrl+Alt+Q
Global::F9
Global::Ctrl+Shift+F9
Global::Ctrl+Shift+F10
Global::Alt+Num *
Global::Ctrl+Shift+F
Global::Ctrl+Shift+H
Global::F8
Global::Shift+F8
Global::Ctrl+PgDn
Global::Ctrl+PgUp
HTML Editor Design View::Ctrl+PgUp
HTML Editor Source View::Ctrl+PgUp
Global::Shift+Esc
Global::Esc
Global::Ctrl+F2
Global::F5
Global::Ctrl+Shift+F5
Global::Alt+F6
Global::Shift+Alt+F6
Global::Ctrl+\, E
Global::Ctrl+\, Ctrl+E
Global::Ctrl+S
HTML Editor Source View::Shift+F7
Settings Designer::F7
Class Diagram::Enter
Global::Ctrl+Shift+W
Global::F2
Global::Ctrl+Alt+E
Global::F7
Global::Alt+F3, S
Global::Ctrl+F5
Global::F3
Global::Shift+F3
Global::Ctrl+F3
Global::Ctrl+Shift+F3
Global::Ctrl+F9
Global::F1
Global::Alt+Enter
Global::Ctrl+/
Global::Ctrl+Shift+C
Global::F6
Global::Shift+F6
Global::Ctrl+F6
Global::Ctrl+Shift+F6
Global::Ctrl+Shift+V
Global::Ctrl+Shift+Ins
Global::Ctrl+F4
Work Item Editor::Esc
Global::Ctrl+Alt+A
Global::Ctrl+Alt+V, A
Global::Ctrl+-
Global::Ctrl+Shift+-
Global::Ctrl+Shift+B
Global::Ctrl+Break
Global::F12
Global::Ctrl+F12
Global::Ctrl+Alt+Ins
Global::Alt+F12
Global::Ctrl+Alt+F12
Global::Ctrl+Shift+E
Global::Shift+F1
HTML Editor Design View::Ctrl+PgDn
HTML Editor Source View::Ctrl+PgDn
Global::Ctrl+Alt+P
Global::Ctrl+Shift+8
Global::Ctrl+Shift+1
Global::Ctrl+Shift+2
Global::Shift+Alt+F12
Global::Ctrl+K, Ctrl+W
Global::Ctrl+K, Ctrl+B
Global::Ctrl+Tab
Global::Ctrl+Shift+Tab
Global::Ctrl+Shift+7
Global::Shift+F12
Global::Alt+-
Global::Ctrl+Shift+Alt+F12, Ctrl+Shift+Alt+F12
Global::Ctrl+Shift+.
Global::Ctrl+Shift+,
Text Editor::Ctrl+M, Ctrl+X
Text Editor::Ctrl+M, Ctrl+A
Text Editor::Ctrl+M, Ctrl+E
Text Editor::Ctrl+M, Ctrl+S
Global::Ctrl+Shift+F12
Global::Ctrl+Alt+R
Global::Ctrl+F1
Global::Ctrl+Alt+F1
Global::Alt+F5
Global::Ctrl+Alt+F5
Global::Ctrl+Q
Global::Ctrl+Alt+B
Global::Ctrl+Alt+D
Global::Ctrl+Alt+G
Global::Ctrl+Alt+U
Global::Alt+F10
Global::Ctrl+Alt+Z
Global::Ctrl+9
Global::Ctrl+B
Global::Ctrl+Alt+F11
Global::Ctrl+Alt+F10
Global::Ctrl+Shift+Alt+F11
Global::Ctrl+8
Global::Shift+Alt+F11
Global::Alt+F9, L
Global::Alt+F9, D
Global::Alt+F9, S
Global::Alt+F9, A
Global::Ctrl+F11
Global::Ctrl+Alt+M, 1
Global::Ctrl+Alt+W, 1
Global::Ctrl+Alt+M, 2
Global::Ctrl+Alt+W, 2
Global::Ctrl+Alt+M, 3
Global::Ctrl+Alt+W, 3
Global::Ctrl+Alt+M, 4
Global::Ctrl+Alt+W, 4
VC Image Editor::Ctrl+Shift+U
VC Image Editor::Ctrl+U
Global::Ctrl+M, Ctrl+V
Global::Ctrl+M, Ctrl+C
Global::Ctrl+M, Ctrl+G
HTML Editor Source View::Ctrl+M, Ctrl+G
Global::Ctrl+Alt+K
Global::Ctrl+Alt+Y, F
Global::Ctrl+Shift+X
Global::Ctrl+Alt+Y, T
Global::Ctrl+,
DataSet Editor::Ctrl+L
DataSet Editor::Ins
XML Schema Designer::Ctrl+1
XML Schema Designer::Ctrl+2
XML Schema Designer::Ctrl+3
XML Schema Designer::Alt+Right Arrow
XML Schema Designer::Alt+Left Arrow
XML Schema Designer::Alt+Down Arrow
XML Schema Designer::Alt+Up Arrow
XML Schema Designer::Del
Global::Ctrl+R, F
Global::Ctrl+R, D
Global::Ctrl+R, C
Global::Ctrl+R, Ctrl+C
Global::Ctrl+R, N
Global::Ctrl+R, Ctrl+N
Global::Ctrl+R, T
Global::Ctrl+R, A
Global::Ctrl+R, Y
Global::Ctrl+R, Ctrl+T
Global::Ctrl+R, Ctrl+A
Global::Ctrl+R, Ctrl+Y
Global::Ctrl+R, Ctrl+F
Global::Ctrl+R, Ctrl+D
Workflow Designer::Ctrl+E, Ctrl+P
Workflow Designer::Ctrl+E, P
Workflow Designer::Ctrl+E, Ctrl+E
Workflow Designer::Ctrl+E, E
Workflow Designer::Ctrl+E, Ctrl+C
Workflow Designer::Ctrl+E, C
Workflow Designer::Ctrl+E, Ctrl+X
Workflow Designer::Ctrl+E, X
Workflow Designer::Ctrl+Alt+F6
Workflow Designer::Ctrl+E, Ctrl+A
Workflow Designer::Ctrl+E, A
Workflow Designer::Ctrl+E, Ctrl+V
Workflow Designer::Ctrl+E, V
Workflow Designer::Ctrl+E, Ctrl+I
Workflow Designer::Ctrl+E, I
Workflow Designer::Ctrl+E, Ctrl+O
Workflow Designer::Ctrl+E, O
Workflow Designer::Ctrl+E, Ctrl+N
Workflow Designer::Ctrl+E, N
Workflow Designer::Ctrl+E, Ctrl+F
Workflow Designer::Ctrl+E, F
Workflow Designer::Ctrl+Num +
Workflow Designer::Ctrl+Num -
Workflow Designer::Ctrl+E, Ctrl+M
Workflow Designer::Ctrl+E, M
Workflow Designer::Ctrl+E, Ctrl+S
Workflow Designer::Ctrl+E, S
F# Interactive::Ctrl+.
Global::Alt+Up Arrow
Global::Alt+Down Arrow
Graph Document Editor::I
Graph Document Editor::O
Graph Document Editor::B
Sequence Diagram::F12
UML Use Case Diagram::Shift+Del
UML Class Diagram::Shift+Del
UML Component Diagram::Shift+Del
Global::Ctrl+\, Ctrl+N
HTML Editor Source View::Ctrl+Shift+J
HTML Editor Design View::Ctrl+Alt+Left Arrow
HTML Editor Design View::Ctrl+Alt+Right Arrow
HTML Editor Design View::Ctrl+Alt+Up Arrow
HTML Editor Design View::Ctrl+Alt+Down Arrow
HTML Editor Design View::Ctrl+Shift+N
HTML Editor Source View::Ctrl+Shift+Y
XML (Text) Editor::Alt+F5
XML (Text) Editor::Ctrl+Alt+F5
Global::Alt+F2
Global::Shift+Alt+D
Query Designer::Ctrl+Shift+J
View Designer::Ctrl+Shift+J
Query Designer::Ctrl+T
View Designer::Ctrl+T
Windows Forms Designer::Shift+Esc
Report Designer::Shift+Esc
Global::Ctrl+Shift+D, K
Global::Ctrl+Shift+D, S
Report Designer::Shift+Space
Report Designer::Ctrl+Space
Report Designer::Ctrl+Alt+D
VisualStudio::Ctrl+1
Global::Ctrl+;
Global::Ctrl+'
Global::Shift+Alt+3
Global::Shift+Alt+4
Global::Ctrl+Shift+F11
Text Editor::Alt+PgUp
Text Editor::Alt+PgDn
Work Item Editor::F5
Work Item Results View::Shift+Alt+P
Work Item Results View::Shift+Alt+N
Work Item Query View::F5
Work Item Results View::F5
Work Item Query View::Shift+Alt+C
Work Item Results View::Shift+Alt+C
Work Item Editor::Shift+Alt+C
Work Item Query View::Shift+Alt+L
Work Item Results View::Shift+Alt+L
Work Item Editor::Shift+Alt+L
Work Item Query View::Shift+Alt+Left Arrow
Work Item Results View::Shift+Alt+Left Arrow
Work Item Query View::Shift+Alt+Right Arrow
Work Item Results View::Shift+Alt+Right Arrow
Work Item Query View::Shift+Alt+V
Work Item Results View::Shift+Alt+V
Global::Ctrl+Shift+P
Global::Ctrl+Shift+R
Global::Alt+F8
Global::Alt+F11
Managed Resources Editor::Ctrl+1
Managed Resources Editor::Ctrl+2
Managed Resources Editor::Ctrl+3
Managed Resources Editor::Ctrl+4
Managed Resources Editor::Ctrl+5
Managed Resources Editor::Ctrl+6
Class Diagram::Shift+Alt+B
Class Diagram::Shift+Alt+L
Class Diagram::Num -
Class Diagram::Num +
Class Diagram::Del
Global::Ctrl+\, Ctrl+C
Global::Ctrl+\, Ctrl+A
Global::Ctrl+R, Q
Data Generator::F5
Schema Compare::Shift+Alt+.
Schema Compare::Shift+Alt+,
Transact-SQL Editor::Ctrl+Shift+E
Transact-SQL Editor::Ctrl+F5
Transact-SQL Editor::Alt+Break
Transact-SQL Editor::Ctrl+Shift+D
Transact-SQL Editor::Ctrl+T
Transact-SQL Editor::Ctrl+Shift+T
Transact-SQL Editor::Ctrl+F6
Transact-SQL Editor::Ctrl+Shift+F6
Transact-SQL Editor::Ctrl+Shift+Alt+R
Transact-SQL Editor::Ctrl+N
Transact-SQL Editor::Ctrl+K, Ctrl+R
Transact-SQL Editor::Ctrl+K, R
Global::Ctrl+D
Global::Ctrl+5
Global::Ctrl+6
Global::Ctrl+7
Transact-SQL Editor::Ctrl+J
";

        #endregion

        internal static readonly string[] SampleCommands = SampleCommandString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        [Fact]
        public void Parse1()
        {
            var b = KeyBinding.Parse("foo::e");
            Assert.Equal("foo", b.Scope);
            Assert.Equal('e', b.FirstKeyStroke.KeyInput.Char);
        }

        [Fact]
        public void Parse2()
        {
            var b = KeyBinding.Parse("::b");
            Assert.Equal(String.Empty, b.Scope);
            Assert.Equal('b', b.FirstKeyStroke.Char);
        }

        [Fact]
        public void Parse3()
        {
            var b = KeyBinding.Parse("::f2");
            Assert.Equal(Char.MinValue, b.FirstKeyStroke.Char);
            Assert.Equal(VimKey.F2, b.FirstKeyStroke.KeyInput.Key);
        }

        /// <summary>
        /// Parse a keybinding with , correctly
        /// </summary>
        [Fact]
        public void Parse4()
        {
            var b = KeyBinding.Parse("::,");
            Assert.Equal(',', b.FirstKeyStroke.Char);
            Assert.Equal(VimKeyModifiers.None, b.FirstKeyStroke.KeyModifiers);
        }

        /// <summary>
        /// Double modifier
        /// </summary>
        [Fact]
        public void Parse5()
        {
            var b = KeyBinding.Parse("::ctrl+shift+f");
            Assert.Equal('f', b.FirstKeyStroke.Char);
            Assert.True(0 != (VimKeyModifiers.Shift & b.FirstKeyStroke.KeyModifiers));
            Assert.True(0 != (VimKeyModifiers.Control & b.FirstKeyStroke.KeyModifiers));
        }

        /// <summary>
        /// Don't carry shift keys for letters
        /// </summary>
        [Fact]
        public void Parse6()
        {
            var b = KeyBinding.Parse("::ctrl+D");
            Assert.Equal('d', b.FirstKeyStroke.Char);
            Assert.Equal(VimKeyModifiers.Control, b.FirstKeyStroke.KeyModifiers);
        }

        [Fact]
        public void ParseMultiple1()
        {
            var b = KeyBinding.Parse("::e, f");
            Assert.Equal(2, b.KeyStrokes.Count());
        }

        /// <summary>
        /// With a comma key
        /// </summary>
        [Fact]
        public void ParseMultiple2()
        {
            var b = KeyBinding.Parse("::,, f");
            Assert.Equal(2, b.KeyStrokes.Count());
            Assert.Equal(',', b.KeyStrokes.ElementAt(0).Char);
            Assert.Equal('f', b.KeyStrokes.ElementAt(1).Char);
        }

        [Fact]
        public void BadParse1()
        {
            Assert.Throws<ArgumentException>(() => KeyBinding.Parse("::notavalidkey"));
        }

        [Fact]
        public void BadParse2()
        {
            Assert.Throws<ArgumentException>(() => KeyBinding.Parse("::ctrl+notavalidkey"));
        }

        [Fact]
        public void VsKeyBackSpace()
        {
            var b = KeyBinding.Parse("::Bkspce");
            Assert.Equal(VimKey.Back, b.FirstKeyStroke.KeyInput.Key);
        }

        [Fact]
        public void VsKeyLeftArrow()
        {
            var b = KeyBinding.Parse("::Left Arrow");
            Assert.Equal(VimKey.Left, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsKeyRightArrow()
        {
            var b = KeyBinding.Parse("::Right Arrow");
            Assert.Equal(VimKey.Right, b.FirstKeyStroke.KeyInput.Key);
        }

        [Fact]
        public void VsKeyUpArrow()
        {
            var b = KeyBinding.Parse("::Up Arrow");
            Assert.Equal(VimKey.Up, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsKeyDownArrow()
        {
            var b = KeyBinding.Parse("::Down Arrow");
            Assert.Equal(VimKey.Down, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsKeyDownArrow2()
        {
            var b = KeyBinding.Parse("::Shift+Down Arrow");
            Assert.Equal(VimKey.Down, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsKeyPageDown()
        {
            var b = KeyBinding.Parse("::PgDn");
            Assert.Equal(VimKey.PageDown, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsKeyPageUp()
        {
            var b = KeyBinding.Parse("::PgUp");
            Assert.Equal(VimKey.PageUp, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsNum1()
        {
            var b = KeyBinding.Parse("::Num +");
            Assert.Equal(VimKey.KeypadPlus, b.FirstKeyStroke.Key);
        }

        [Fact]
        public void VsNum2()
        {
            var b = KeyBinding.Parse("::Num *");
            Assert.Equal(VimKey.KeypadMultiply, b.FirstKeyStroke.Key);
        }

        /// <summary>
        /// Ensure we can parse all available Visual Studio commands
        /// </summary>
        [Fact]
        public void ParseAllVsCommands()
        {
            foreach (var line in SampleCommands)
            {
                Assert.True(KeyBinding.TryParse(line, out KeyBinding binding), "Could not parse - " + line);
            }
        }

        /// <summary>
        /// Ensure the re-generated strings all match the original
        /// </summary>
        [Fact]
        public void CommandStringAllVsCommands()
        {
            foreach (var line in SampleCommands)
            {
                Assert.True(KeyBinding.TryParse(line, out KeyBinding binding));
                Assert.Equal(line, binding.CommandString);
            }
        }

        [Fact]
        public void Equality1()
        {
            var value = EqualityUnit
                .Create(KeyBinding.Parse("::b"))
                .WithEqualValues(KeyBinding.Parse("::b"))
                .WithNotEqualValues(KeyBinding.Parse("local::b"))
                .WithNotEqualValues(KeyBinding.Parse("::Shift+b"))
                .WithNotEqualValues(KeyBinding.Parse("::Left Arrow"));
            EqualityUtil.RunAll(
                (x, y) => x == y,
                (x, y) => x != y,
                values: value);
        }
    }
}
