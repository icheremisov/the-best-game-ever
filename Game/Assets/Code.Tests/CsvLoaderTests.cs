using NUnit.Framework;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class CsvLoaderTests
    {
        [Test]
        public void ParseLine_SimpleCommas()
        {
            var fields = CsvLoader.ParseLine("a,b,c");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, fields);
        }

        [Test]
        public void ParseLine_QuotedFieldWithComma()
        {
            var fields = CsvLoader.ParseLine("a,\"b,c\",d");
            CollectionAssert.AreEqual(new[] { "a", "b,c", "d" }, fields);
        }

        [Test]
        public void ParseLine_EmptyFieldsInMiddle()
        {
            var fields = CsvLoader.ParseLine("a,,c");
            CollectionAssert.AreEqual(new[] { "a", "", "c" }, fields);
        }

        [Test]
        public void ParseLine_TrailingEmpty()
        {
            var fields = CsvLoader.ParseLine("a,b,");
            CollectionAssert.AreEqual(new[] { "a", "b", "" }, fields);
        }

        [Test]
        public void ParseAll_SkipsHeader_AndBlankLines()
        {
            string csv = "h1,h2\na,1\n\nb,2\n";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "1" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "b", "2" }, rows[1]);
        }
    }
}
