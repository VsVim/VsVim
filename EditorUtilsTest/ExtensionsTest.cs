using Microsoft.VisualStudio.Utilities;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class ExtensionsTest
    {
        [Test]
        public void TryGetPropertySafe_Found()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, "target");

            string value;
            Assert.IsTrue(col.TryGetPropertySafe(key, out value));
            Assert.AreEqual("target", value);
        }

        [Test]
        public void TryGetPropertySafe_NotFound()
        {
            var col = new PropertyCollection();
            var key = new object();

            string value;
            Assert.IsFalse(col.TryGetPropertySafe(key, out value));
        }

        /// <summary>
        /// Make sure it doesn't throw if the value is the wrong type
        /// </summary>
        [Test]
        public void TryGetPropertySafe_WrongType()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, this);

            string value;
            Assert.IsFalse(col.TryGetPropertySafe(key, out value));
        }

    }
}
