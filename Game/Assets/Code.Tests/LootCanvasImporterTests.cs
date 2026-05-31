using System.Collections.Generic;
using NUnit.Framework;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class LootCanvasImporterTests
    {
        // Парсит сгенерированный рантайм-CSV и возвращает строку предмета по id.
        private static Dictionary<string, string> Row(string runtimeCsv, string id)
        {
            string[] cols =
            {
                "id","name","description","shape","gold","acidCost","healOnDigest",
                "cellsRestoredOnDigest","adjacencyEffect","category","acidRestoreOnDigest",
                "damageOnDigest","canReturnToBasket","glue","neighborGoldPct"
            };
            foreach (var r in CsvLoader.ParseAll(runtimeCsv))
            {
                if (r.Length > 0 && r[0] == id)
                {
                    var d = new Dictionary<string, string>();
                    for (int i = 0; i < cols.Length; i++) d[cols[i]] = i < r.Length ? r[i] : "";
                    return d;
                }
            }
            return null;
        }

        [Test]
        public void Build_SingleCell_ShapeFromCanvas_AndDefaults()
        {
            string sheet =
                "id,name,description,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyEffect,shape,,\n" +
                "gem,Самоцвет,Блеск,20,2,0,0,gem|gold:+25%,TRUE,,\n";

            var row = Row(LootCanvasImporter.BuildLootCsv(sheet), "gem");

            Assert.IsNotNull(row, "gem должен присутствовать");
            Assert.AreEqual("X", row["shape"]);
            Assert.AreEqual("20", row["gold"]);
            Assert.AreEqual("2", row["acidCost"]);
            Assert.AreEqual("gem|gold:+25%", row["adjacencyEffect"]);
            // незаданные blue-loop колонки берут дефолты:
            Assert.AreEqual("normal", row["category"]);
            Assert.AreEqual("1", row["canReturnToBasket"]);
            Assert.AreEqual("0", row["neighborGoldPct"]);
        }

        [Test]
        public void Build_MultiRowCanvas_BoundingBoxShape()
        {
            string sheet =
                "id,name,description,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyEffect,shape,,\n" +
                "shield,Щит,Защита,8,4,5,0,sword|gold:+50%,TRUE,TRUE,\n" +
                ",,,,,,,,TRUE,TRUE,\n";

            var row = Row(LootCanvasImporter.BuildLootCsv(sheet), "shield");

            Assert.IsNotNull(row);
            Assert.AreEqual("XX|XX", row["shape"]);
            Assert.AreEqual("sword|gold:+50%", row["adjacencyEffect"]);
        }

        [Test]
        public void Build_MultiLineAdjacencyEffect_PreservedAndCanvasRowsSeparate()
        {
            // adjacencyEffect — многострочная ячейка в кавычках; предмет занимает 4 строки канваса.
            string sheet =
                "id,name,description,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyEffect,shape,,\n" +
                "barracuda,Барракуда,Трепых,15,5,0,2,\"bread|acid:-30%;\ngem|gold:+25%*\",FALSE,TRUE,\n" +
                ",,,,,,,,TRUE,TRUE,\n" +
                ",,,,,,,,FALSE,TRUE,\n" +
                ",,,,,,,,FALSE,TRUE,\n";

            var row = Row(LootCanvasImporter.BuildLootCsv(sheet), "barracuda");

            Assert.IsNotNull(row);
            Assert.AreEqual(".X|XX|.X|.X", row["shape"]);
            // перенос строки внутри adjacencyEffect сохраняется (рантайм-парсер его нормализует в пробел):
            Assert.AreEqual("bread|acid:-30%;\ngem|gold:+25%*", row["adjacencyEffect"]);
        }

        [Test]
        public void Build_BlueLoopColumns_MappedByName()
        {
            string sheet =
                "id,name,description,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyEffect,category,damageOnDigest,neighborGoldPct,shape,,\n" +
                "poop,Какашка,Портит,0,4,0,0,stomach|acid:+50%,punish,8,-50,TRUE,,\n";

            var row = Row(LootCanvasImporter.BuildLootCsv(sheet), "poop");

            Assert.IsNotNull(row);
            Assert.AreEqual("X", row["shape"]);
            Assert.AreEqual("punish", row["category"]);
            Assert.AreEqual("8", row["damageOnDigest"]);
            Assert.AreEqual("-50", row["neighborGoldPct"]);
        }

        [Test]
        public void Build_ColumnsMappedByName_OrderIndependent()
        {
            // Колонки в произвольном порядке (name после description, gold позже) — маппинг по имени.
            string sheet =
                "id,description,name,adjacencyEffect,gold,acidCost,healOnDigest,cellsRestoredOnDigest,shape,,\n" +
                "sword,Острый,Меч,,10,3,0,0,TRUE,,\n" +
                ",,,,,,,,TRUE,,\n" +
                ",,,,,,,,TRUE,,\n" +
                ",,,,,,,,TRUE,,\n";

            var row = Row(LootCanvasImporter.BuildLootCsv(sheet), "sword");

            Assert.IsNotNull(row);
            Assert.AreEqual("Меч", row["name"]);
            Assert.AreEqual("Острый", row["description"]);
            Assert.AreEqual("10", row["gold"]);
            Assert.AreEqual("X|X|X|X", row["shape"]);
        }
    }
}
