using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class MotionCaptureTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        [SetUp]
        public void Init()
        {
            Create(s_lines);
        }

        public void Create(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        internal MotionResult Process(int startPosition, int count, string input)
        {
            Assert.IsTrue(count > 0, "this will cause an almost infinite loop");
            var res = MotionCapture.ProcessInput(
                new SnapshotPoint(_snapshot, startPosition),
                InputUtil.CharToKeyInput(input[0]),
                count);
            foreach (var cur in input.Skip(1))
            {
                Assert.IsTrue(res.IsNeedMoreInput);
                var needMore = (MotionResult.NeedMoreInput)res;
                res = needMore.Item.Invoke(InputUtil.CharToKeyInput(cur));
            }

            return res;
       }

        internal void ProcessComplete(int startPosition, int count, string input, string match, MotionKind motionKind, OperationKind opKind)
        {
            var res = Process(startPosition, count, input);
            var tuple = res.AsComplete().Item;
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual(match, tuple.Span.GetText());
            Assert.AreEqual(motionKind, tuple.MotionKind);
            Assert.AreEqual(opKind, tuple.OperationKind);
        }


        [Test]
        public void Word1()
        {
            Create("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), InputUtil.CharToKeyInput('w'), 1);
            Assert.IsTrue(res.IsComplete);
            var res2 = (MotionResult.Complete)res;
            var span = res2.Item.Span;
            Assert.AreEqual(4, span.Length);
            Assert.AreEqual("foo ", span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, res2.Item.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res2.Item.OperationKind);
        }


        [Test]
        public void Word2()
        {
            Create("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 1), InputUtil.CharToKeyInput('w'), 1);
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Span;
            Assert.AreEqual(3, span.Length);
            Assert.AreEqual("oo ", span.GetText());
        }

        [Test, Description("Word motion with a count")]
        public void Word3()
        {
            Create("foo bar baz");
            var res = Process(0, 2, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar ", res.AsComplete().Item.Span.GetText());
        }

        [Test, Description("Count across lines")]
        public void Word4()
        {
            Create("foo bar", "baz jaz");
            var res = Process(0, 3, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar" + Environment.NewLine + "baz ", res.AsComplete().Item.Span.GetText());
        }

        [Test, Description("Count off the end of the buffer")]
        public void Word5()
        {
            Create("foo bar");
            var res = Process(0, 10, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar", res.AsComplete().Item.Span.GetText());
        }


        [Test]
        public void BadInput()
        {
            Create("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), InputUtil.CharToKeyInput('z'), 0);
            Assert.IsTrue(res.IsInvalidMotion);
            res = res.AsInvalidMotion().Item2.Invoke(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            Assert.IsTrue(res.IsCancel);
        }


        [Test, Description("Keep getting input until it's escaped")]
        public void BadInput2()
        {
            Create("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), InputUtil.CharToKeyInput('z'), 0);
            Assert.IsTrue(res.IsInvalidMotion);
            res = res.AsInvalidMotion().Item2.Invoke(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(res.IsInvalidMotion);
            res = res.AsInvalidMotion().Item2.Invoke(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            Assert.IsTrue(res.IsCancel);
        }

        [Test]
        public void EndOfLine1()
        {
            Create("foo bar", "baz");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 0);
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Span;
            Assert.AreEqual("foo bar", span.GetText());
            var tuple = res.AsComplete().Item;
            Assert.AreEqual(MotionKind.Inclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test]
        public void EndOfLine2()
        {
            Create("foo bar", "baz");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 1), ki, 0);
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Span;
            Assert.AreEqual("oo bar", span.GetText());
        }

        [Test]
        public void EndOfLineCount1()
        {
            Create("foo", "bar", "baz");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 2);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo" + Environment.NewLine + "bar", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test]
        public void EndOfLineCount2()
        {
            Create("foo", "bar", "baz", "jar");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 3);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine +"baz", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test,Description("Make sure counts past the end of the buffer don't crash")]
        public void EndOfLineCount3()
        {
            Create("foo");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 300);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo", tuple.Span.GetText());
        }

        [Test]
        public void StartOfLine1()
        {
            Create("foo");
            var ki = InputUtil.CharToKeyInput('^');
            var res = MotionCapture.ProcessInput(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, ki, 1);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure it goes to the first non-whitespace character")]
        public void StartOfLine2()
        {
            Create("  foo");
            var ki = InputUtil.CharToKeyInput('^');
            var res = MotionCapture.ProcessInput(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, ki, 1);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test]
        public void Count1()
        {
            Create("foo bar baz");
            var res  = Process(0, 1, "2w");
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Span;
            Assert.AreEqual("foo bar ", span.GetText());
        }

        [Test, Description("Count of 1")]
        public void Count2()
        {
            Create("foo bar baz");
            var res = Process(0, 1, "1w");
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Span;
            Assert.AreEqual("foo ", span.GetText());
        }

        [Test]
        public void AllWord1()
        {
            Create("foo bar");
            var res = Process(0, 1, "aw");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo ", res.AsComplete().Item.Span.GetText());
        }

        [Test]
        public void AllWord2()
        {
            Create("foo bar");
            var res = Process(1, 1, "aw");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo ", res.AsComplete().Item.Span.GetText());
        }

        [Test]
        public void AllWord3()
        {
            Create("foo bar baz");
            var res = Process(1, 1, "2aw");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar ", res.AsComplete().Item.Span.GetText());
        }

        [Test]
        public void CharLeft1()
        {
            Create("foo bar");
            var res = Process(2, 1, "2h");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("fo", res.AsComplete().Item.Span.GetText());
        }

        [Test, Description("Make sure that counts are multiplied")]
        public void CharLeft2()
        {
            Create("food bar");
            var res = Process(4, 2, "2h");
            Assert.AreEqual("food", res.AsComplete().Item.Span.GetText());
        }

        [Test]
        public void CharRight1()
        {
            Create("foo");
            var res = Process(0, 1, "2l");
            Assert.AreEqual("fo", res.AsComplete().Item.Span.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, res.AsComplete().Item.OperationKind);
        }

        [Test]
        public void LineUp1()
        {
            Create("foo", "bar");
            var res = Process(_snapshot.GetLineFromLineNumber(1).Start.Position, 1, "k");
            Assert.AreEqual(OperationKind.LineWise, res.AsComplete().Item.OperationKind);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.AsComplete().Item.Span.GetText());
        }

        [Test]
        public void EndOfWord1()
        {
            Create("foo bar");
            var res = Process(new SnapshotPoint(_snapshot, 0), 1, "e").AsComplete().Item;
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, 3), res.Span);
        }

        [Test, Description("Needs to cross the end of the line")]
        public void EndOfWord2()
        {
            Create("foo   ","bar");
            var point = new SnapshotPoint(_snapshot, 4);
            var res = Process(point, 1, "e").AsComplete().Item;
            var span = new SnapshotSpan(
                point,
                _snapshot.GetLineFromLineNumber(1).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfWord3()
        {
            Create("foo bar baz jaz");
            var res = Process(new SnapshotPoint(_snapshot, 0), 2, "e").AsComplete().Item;
            var span = new SnapshotSpan(_snapshot, 0, 7);
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Work across blank lines")]
        public void EndOfWord4()
        {
            Create("foo   ", "", "bar");
            var point = new SnapshotPoint(_snapshot, 4);
            var res = Process(point, 1, "e").AsComplete().Item;
            var span = new SnapshotSpan(
                point,
                _snapshot.GetLineFromLineNumber(2).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Go off the end of the buffer")]
        public void EndOfWord5()
        {
            Create("foo   ", "", "bar");
            var point = new SnapshotPoint(_snapshot, 4);
            var res = Process(point, 400, "e").AsComplete().Item;
            var span = new SnapshotSpan(
                point,
                SnapshotUtil.GetEndPoint(_snapshot));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void ForwardChar1()
        {
            Create("foo bar baz");
            ProcessComplete(0,1,"fo", "fo", MotionKind.Inclusive, OperationKind.CharacterWise);
            ProcessComplete(1,1,"fo", "oo", MotionKind.Inclusive, OperationKind.CharacterWise);
            ProcessComplete(1,1,"fb", "oo b", MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void ForwardChar2()
        {
            Create("foo bar baz");
            var res = Process(0, 1, "fq");
            Assert.IsTrue(res.IsError);
        }

        [Test]
        public void ForwardChar3()
        {
            Create("foo bar baz");
            ProcessComplete(0, 2, "fo", "foo", MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test,Description("Bad count gets nothing in gVim")]
        public void ForwardChar4()
        {
            Create("foo bar baz");
            var res = Process(0, 300, "fo");
            Assert.IsTrue(res.IsError);
        }

        [Test]
        public void ForwardTillChar1()
        {
            Create("foo bar baz");
            ProcessComplete(0,1,"to", "f", MotionKind.Inclusive, OperationKind.CharacterWise);
            ProcessComplete(1,1,"tb", "oo ", MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void ForwardTillChar2()
        {
            Create("foo bar baz");
            var res = Process(0, 1, "tq");
            Assert.IsTrue(res.IsError);
        }

        [Test]
        public void ForwardTillChar3()
        {
            Create("foo bar baz");
            ProcessComplete(0, 2, "to", "fo", MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test,Description("Bad count gets nothing in gVim")]
        public void ForwardTillChar4()
        {
            Create("foo bar baz");
            var res = Process(0, 300, "to");
            Assert.IsTrue(res.IsError);
        }

        [Test]
        public void OperationSpan1()
        {
            Create("foo bar", "baz");
            var data = Process(4, 1, "w").AsComplete().Item;
            Assert.AreEqual("bar" + Environment.NewLine, data.Span.GetText());
            Assert.AreEqual("bar", data.OperationSpan.GetText());
        }

        [Test]
        public void OperationSpan2()
        {
            Create("foo bar", "  baz");
            var data = Process(4, 1, "w").AsComplete().Item;
            Assert.AreEqual("bar" + Environment.NewLine + "  ", data.Span.GetText());
            Assert.AreEqual("bar", data.OperationSpan.GetText());
        }

        [Test]
        public void BackwardCharMotion1()
        {
            Create("the boy kicked the ball");
            var data = Process(_buffer.GetLine(0).End.Position, 1, "Fb").AsComplete().Item;
            Assert.AreEqual("ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardCharMotion2()
        {
            Create("the boy kicked the ball");
            var data = Process(_buffer.GetLine(0).End.Position, 2, "Fb").AsComplete().Item;
            Assert.AreEqual("boy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion1()
        {
            Create("the boy kicked the ball");
            var data = Process(_buffer.GetLine(0).End.Position, 1, "Tb").AsComplete().Item;
            Assert.AreEqual("all", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion2()
        {
            Create("the boy kicked the ball");
            var data = Process(_buffer.GetLine(0).End.Position, 2, "Tb").AsComplete().Item;
            Assert.AreEqual("oy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

    }   
    
}
