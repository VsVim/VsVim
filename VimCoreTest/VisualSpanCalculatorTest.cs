using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VisualSpanCalculatorTest
    {
        private VisualSpanCalculator _calcRaw;
        private IVisualSpanCalculator _calc;

        [SetUp]
        public void Setup()
        {
            _calcRaw = new VisualSpanCalculator();
            _calc = _calcRaw;
        }

        [Test]
        public void CalculateForBlock1()
        {
            var buffer = EditorUtil.CreateBuffer("dog", "cat", "chicken");
            var col = new NormalizedSnapshotSpanCollection(buffer.GetSpan(0, 2));
            var span = _calcRaw.CalculateForBlock(buffer.GetPoint(0), col);
            Assert.IsTrue(span.IsMultiple);
            Assert.AreEqual(col[0], span.AsMultiple().Item2[0]);
        }

        [Test]
        public void CalculateForBlock2()
        {
            var buffer = EditorUtil.CreateBuffer("dog", "cat", "chicken");
            var col = new NormalizedSnapshotSpanCollection(buffer.GetSpan(0, 2));
            var span = _calcRaw.CalculateForBlock(buffer.GetLine(1).Start, col);
            Assert.IsTrue(span.IsMultiple);
            Assert.AreEqual(buffer.GetLine(1).Start.GetSpan(2), span.AsMultiple().Item2[0]);
        }

        [Test]
        public void CalculateForBlock3()
        {
            var buffer = EditorUtil.CreateBuffer("dog", "cat", "chicken");
            var col = new NormalizedSnapshotSpanCollection(buffer.GetSpan(0, 2));
            var span = _calcRaw.CalculateForBlock(buffer.GetLine(1).Start.Add(1), col);
            Assert.IsTrue(span.IsMultiple);
            Assert.AreEqual(buffer.GetLine(1).Start.Add(1).GetSpan(2), span.AsMultiple().Item2[0]);
        }

        [Test]
        public void CalculateForBlock4()
        {
            var buffer = EditorUtil.CreateBuffer("dog again", "cat again", "chicken");
            var col = new NormalizedSnapshotSpanCollection(new SnapshotSpan[] 
            {
                buffer.GetLine(0).Start.GetSpan(2),
                buffer.GetLine(1).Start.GetSpan(2) 
            });
            var span = _calcRaw.CalculateForBlock(buffer.GetLine(1).Start, col);
            Assert.IsTrue(span.IsMultiple);
            col = span.AsMultiple().item2;
            Assert.AreEqual(buffer.GetLine(1).Start.GetSpan(2), col[0]);
            Assert.AreEqual(buffer.GetLine(2).Start.GetSpan(2), col[1]);
        }
    }
}
