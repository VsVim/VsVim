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
            Initialize(s_lines);
        }

        public void Initialize(params string[] lines)
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


        [Test]
        public void Word1()
        {
            Initialize("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), InputUtil.CharToKeyInput('w'), 1);
            Assert.IsTrue(res.IsComplete);
            var res2 = (MotionResult.Complete)res;
            var span = res2.Item.Item1;
            Assert.AreEqual(4, span.Length);
            Assert.AreEqual("foo ", span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, res2.Item.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, res2.Item.Item3);
        }


        [Test]
        public void Word2()
        {
            Initialize("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 1), InputUtil.CharToKeyInput('w'), 1);
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Item1;
            Assert.AreEqual(3, span.Length);
            Assert.AreEqual("oo ", span.GetText());
        }

        [Test, Description("Word motion with a count")]
        public void Word3()
        {
            Initialize("foo bar baz");
            var res = Process(0, 2, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar ", res.AsComplete().Item.Item1.GetText());
        }

        [Test, Description("Count across lines")]
        public void Word4()
        {
            Initialize("foo bar", "baz jaz");
            var res = Process(0, 3, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar" + Environment.NewLine + "baz ", res.AsComplete().Item.Item1.GetText());
        }

        [Test, Description("Count off the end of the buffer")]
        public void Word5()
        {
            Initialize("foo bar");
            var res = Process(0, 10, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void Word6()
        {
            Initialize("foo bar", "baz");
            var res = Process(4, 1, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("bar", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void Word7()
        {
            Initialize("foo bar", "  baz");
            var res = Process(4, 1, "w");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("bar", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void BadInput()
        {
            Initialize("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), InputUtil.CharToKeyInput('z'), 0);
            Assert.IsTrue(res.IsInvalidMotion);
            res = res.AsInvalidMotion().Item2.Invoke(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EscapeKey));
            Assert.IsTrue(res.IsCancel);
        }


        [Test, Description("Keep gettnig input until it's escaped")]
        public void BadInput2()
        {
            Initialize("foo bar");
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), InputUtil.CharToKeyInput('z'), 0);
            Assert.IsTrue(res.IsInvalidMotion);
            res = res.AsInvalidMotion().Item2.Invoke(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(res.IsInvalidMotion);
            res = res.AsInvalidMotion().Item2.Invoke(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EscapeKey));
            Assert.IsTrue(res.IsCancel);
        }

        [Test]
        public void EndOfLine1()
        {
            Initialize("foo bar", "baz");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 0);
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Item1;
            Assert.AreEqual("foo bar", span.GetText());
            var tuple = res.AsComplete().Item;
            Assert.AreEqual(MotionKind.Inclusive, tuple.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.Item3);
        }

        [Test]
        public void EndOfLine2()
        {
            Initialize("foo bar", "baz");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 1), ki, 0);
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Item1;
            Assert.AreEqual("oo bar", span.GetText());
        }

        [Test]
        public void EndOfLineCount1()
        {
            Initialize("foo", "bar", "baz");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 2);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo" + Environment.NewLine + "bar", tuple.Item1.GetText());
            Assert.AreEqual(MotionKind.Inclusive, tuple.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.Item3);
        }

        [Test]
        public void EndOfLineCount2()
        {
            Initialize("foo", "bar", "baz", "jar");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 3);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine +"baz", tuple.Item1.GetText());
            Assert.AreEqual(MotionKind.Inclusive, tuple.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.Item3);
        }

        [Test,Description("Make sure counts past the end of the buffer don't crash")]
        public void EndOfLineCount3()
        {
            Initialize("foo");
            var ki = InputUtil.CharToKeyInput('$');
            var res = MotionCapture.ProcessInput(new SnapshotPoint(_snapshot, 0), ki, 300);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo", tuple.Item1.GetText());
        }

        [Test]
        public void StartOfLine1()
        {
            Initialize("foo");
            var ki = InputUtil.CharToKeyInput('^');
            var res = MotionCapture.ProcessInput(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, ki, 1);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo", tuple.Item1.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.Item3);
        }

        [Test, Description("Make sure it goes to the first non-whitespace character")]
        public void StartOfLine2()
        {
            Initialize("  foo");
            var ki = InputUtil.CharToKeyInput('^');
            var res = MotionCapture.ProcessInput(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, ki, 1);
            Assert.IsTrue(res.IsComplete);
            var tuple = res.AsComplete().Item;
            Assert.AreEqual("foo", tuple.Item1.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.Item3);
        }

        [Test]
        public void Count1()
        {
            Initialize("foo bar baz");
            var res  = Process(0, 1, "2w");
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Item1;
            Assert.AreEqual("foo bar ", span.GetText());
        }

        [Test, Description("Count of 1")]
        public void Count2()
        {
            Initialize("foo bar baz");
            var res = Process(0, 1, "1w");
            Assert.IsTrue(res.IsComplete);
            var span = res.AsComplete().Item.Item1;
            Assert.AreEqual("foo ", span.GetText());
        }

        [Test]
        public void AllWord1()
        {
            Initialize("foo bar");
            var res = Process(0, 1, "aw");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo ", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void AllWord2()
        {
            Initialize("foo bar");
            var res = Process(1, 1, "aw");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo ", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void AllWord3()
        {
            Initialize("foo bar baz");
            var res = Process(1, 1, "2aw");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("foo bar ", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void CharLeft1()
        {
            Initialize("foo bar");
            var res = Process(2, 1, "2h");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("fo", res.AsComplete().Item.Item1.GetText());
        }

        [Test, Description("Make sure that counts are multiplied")]
        public void CharLeft2()
        {
            Initialize("food bar");
            var res = Process(4, 2, "2h");
            Assert.AreEqual("food", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void CharRight1()
        {
            Initialize("foo");
            var res = Process(0, 1, "2l");
            Assert.AreEqual("fo", res.AsComplete().Item.Item1.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, res.AsComplete().Item.Item3);
        }

        [Test]
        public void LineUp1()
        {
            Initialize("foo", "bar");
            var res = Process(_snapshot.GetLineFromLineNumber(1).Start.Position, 1, "k");
            Assert.AreEqual(OperationKind.LineWise, res.AsComplete().Item.Item3);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.AsComplete().Item.Item1.GetText());
        }

        [Test]
        public void EndOfWord1()
        {
            Initialize("foo bar");
            var res = Process(new SnapshotPoint(_snapshot, 0), 1, "e").AsComplete().Item;
            Assert.AreEqual(MotionKind.Inclusive, res.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, res.Item3);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, 3), res.Item1);
        }

        [Test, Description("Needs to cross the end of the line")]
        public void EndOfWord2()
        {
            Initialize("foo   ","bar");
            var point = new SnapshotPoint(_snapshot, 4);
            var res = Process(point, 1, "e").AsComplete().Item;
            var span = new SnapshotSpan(
                point,
                _snapshot.GetLineFromLineNumber(1).Start.Add(3));
            Assert.AreEqual(span, res.Item1);
            Assert.AreEqual(MotionKind.Inclusive, res.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, res.Item3);
        }

        [Test]
        public void EndOfWord3()
        {
            Initialize("foo bar baz jaz");
            var res = Process(new SnapshotPoint(_snapshot, 0), 2, "e").AsComplete().Item;
            var span = new SnapshotSpan(_snapshot, 0, 7);
            Assert.AreEqual(span, res.Item1);
            Assert.AreEqual(MotionKind.Inclusive, res.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, res.Item3);
        }

        [Test, Description("Work across blank lines")]
        public void EndOfWord4()
        {
            Initialize("foo   ", "", "bar");
            var point = new SnapshotPoint(_snapshot, 4);
            var res = Process(point, 1, "e").AsComplete().Item;
            var span = new SnapshotSpan(
                point,
                _snapshot.GetLineFromLineNumber(2).Start.Add(3));
            Assert.AreEqual(span, res.Item1);
            Assert.AreEqual(MotionKind.Inclusive, res.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, res.Item3);
        }

        [Test, Description("Go off the end ofthe buffer")]
        public void EndOfWord5()
        {
            Initialize("foo   ", "", "bar");
            var point = new SnapshotPoint(_snapshot, 4);
            var res = Process(point, 400, "e").AsComplete().Item;
            var span = new SnapshotSpan(
                point,
                TssUtil.GetEndPoint(_snapshot));
            Assert.AreEqual(span, res.Item1);
            Assert.AreEqual(MotionKind.Inclusive, res.Item2);
            Assert.AreEqual(OperationKind.CharacterWise, res.Item3);
        }
    }   
}
