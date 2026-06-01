using System.Collections.Generic;
using NUnit.Framework;
using Mimic.Catalogs;
using Mimic.Data;

namespace Mimic.Tests
{
    public class DialogCatalogTests
    {
        [Test]
        public void Parse_GroupsLinesByTriggerUntilNextTrigger()
        {
            var csv = "trigger,text,icon\n"
                    + "start_day_1,Привет!,master\n"
                    + ",Пока!,master\n"
                    + "end_day_1,Итог,master\n";
            var chains = DialogCatalog.Parse(csv);

            Assert.IsTrue(chains.ContainsKey("start_day_1"));
            Assert.AreEqual(2, chains["start_day_1"].Count);
            Assert.AreEqual("Привет!", chains["start_day_1"][0].Text);
            Assert.AreEqual("master", chains["start_day_1"][0].Icon);
            Assert.AreEqual("Пока!", chains["start_day_1"][1].Text);

            Assert.IsTrue(chains.ContainsKey("end_day_1"));
            Assert.AreEqual(1, chains["end_day_1"].Count);
            Assert.AreEqual("Итог", chains["end_day_1"][0].Text);
        }

        [Test]
        public void Get_ReturnsNullForUnknownTrigger()
        {
            DialogCatalog.SetForTest(new Dictionary<string, List<DialogLine>>());
            Assert.IsNull(DialogCatalog.Get("nope"));
        }
    }
}
