using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        [Fact]
        public void Simple()
        {
            List<Game> list = Test("simple.json");

            Assert.True(list.Count == 1);
        }

        [Fact]
        public void InCondition()
        {
            List<Game> list = Test("in.json");

            Assert.True(list.Count == 2);
        }

        [Fact]
        public void GreaterThan()
        {
            List<Game> list = Test("greaterThan.json");
            int total = FakeGameService.GetDatas().Count();

            Assert.True(list.Count > 0 && list.Count < total);
        }


        [Fact]
        public void Complex()
        {
            List<Game> list = Test("complex.json");

            Assert.True(list.Count > 0);
        }


        [Fact]
        public void Guid()
        {
            string rules = GetJsonTestFile("guid.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.False(result);
        }


        [Fact]
        public void Bool()
        {
            string rules = GetJsonTestFile("bool.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }

        [Fact]
        public void Date()
        {
            string rules = GetJsonTestFile("date.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }


        [Fact]
        public void Object()
        {
            string rules = GetJsonTestFile("object.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.Last(), rules);
            Assert.True(result);
        }


        [Fact]
        public void ListContains()
        {
            string rules = GetJsonTestFile("listContains.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }

        [Fact]
        public void ListIn()
        {
            string rules = GetJsonTestFile("listIn.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }
        [Fact]
        public void Empty()
        {
            string rules = GetJsonTestFile("empty.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }

        [Fact]
        public void IsNull()
        {
            string rules = GetJsonTestFile("isNull.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(new Game() { Category = null }, rules);
            Assert.True(result);
        }

        [Fact]
        public void IsNotNull()
        {
            string rules = GetJsonTestFile("isNotNull.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(new Game() { Category = "Titi" }, rules);
            Assert.True(result);
        }


        [Fact]
        public void Evaluate()
        {
            string rules = GetJsonTestFile("complex.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }

        private static List<Game> Test(string jsonRuleFilePath)
        {
            string rules = GetJsonTestFile(jsonRuleFilePath);

            var expression = JsonRuleEngine.ParseExpression<Game>(rules);

            var datas = FakeGameService.GetDatas();
            var list = datas.Where(expression).ToList();
            return list;
        }

        private static string GetJsonTestFile(string jsonRuleFilePath)
        {
            return File.ReadAllText(Path.Combine("TestJsons/", jsonRuleFilePath));
        }
    }
}
