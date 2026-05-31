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

        [Test]
        public void ParseAll_MultiLineQuotedField_KeepsNewlineInField()
        {
            // Поле во второй колонке содержит перенос строки внутри кавычек.
            string csv = "h1,h2\na,\"line1\nline2\"\nb,2\n";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "line1\nline2" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "b", "2" }, rows[1]);
        }

        [Test]
        public void ParseAll_QuotedField_EscapedQuotesCommaAndNewline()
        {
            // Логическое поле: he said "hi", ok<NL>next  — кавычки (""), запятая и перенос внутри одного поля.
            string csv = "h\n\"he said \"\"hi\"\", ok\nnext\",z\n";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(1, rows.Count);
            CollectionAssert.AreEqual(new[] { "he said \"hi\", ok\nnext", "z" }, rows[0]);
        }

        [Test]
        public void ParseAll_BlankLineInsideQuotedField_NotARecordBreak()
        {
            // Пустая строка внутри кавычек не должна обрывать запись.
            string csv = "h\na,\"x\n\ny\"\n";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(1, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "x\n\ny" }, rows[0]);
        }

        [Test]
        public void ParseAll_QuotedMultiLineField_NoTrailingNewline()
        {
            // Многострочное поле в самом конце файла без завершающего перевода строки.
            string csv = "h\na,\"p\nq\"";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(1, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "p\nq" }, rows[0]);
        }

        [Test]
        public void ParseAll_CrLf_MultiLineQuotedField()
        {
            // CRLF-перевод строк, в т.ч. внутри кавычек — нормализуется так же.
            string csv = "h1,h2\r\na,\"l1\r\nl2\"\r\nb,2\r\n";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "l1\nl2" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "b", "2" }, rows[1]);
        }
    }
}
