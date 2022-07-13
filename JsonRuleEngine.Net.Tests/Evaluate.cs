using Newtonsoft.Json;
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
        public void Dictionary()
        {
            var dict = new Dictionary<string, object>() {
                {"1234", "ok" },
                {"1235", "ok2" }
            };
            bool result = JsonRuleEngine.Evaluate(dict, new ConditionRuleSet() { Field = "1234", Operator = ConditionRuleOperator.equal, Value = "ok" });
            Assert.True(result);
        }

        [Fact]
        public void Dictionary_NullValue()
        {
            var dict = new Dictionary<string, object>() {
                {"1234", null },
                {"1235", "ok2" }
            };
            bool result = JsonRuleEngine.Evaluate(dict, new ConditionRuleSet() { Field = "1234", Operator = ConditionRuleOperator.notEqual, Value = "ok" });
            Assert.True(result);
        }


        [Fact]
        public void Dictionary_Advanced()
        {
            var dict = new Dictionary<string, object>() {
                {"fa3cca71-6f90-440a-bad4-217f141cf20c", "da3cca71-6f90-440a-bad4-217f141cf20c" },
                {"1235", "nok" }
            };
            bool result = JsonRuleEngine.Evaluate(dict, new ConditionRuleSet()
            {
                Separator = ConditionRuleSeparator.And,
                Rules = new[] {
                   new ConditionRuleSet<bool>() { Field = "fa3cca71-6f90-440a-bad4-217f141cf20c", Operator = ConditionRuleOperator.equal, Value = "da3cca71-6f90-440a-bad4-217f141cf20c" },
                   new ConditionRuleSet<bool>() { Field = "1235", Operator = ConditionRuleOperator.notEqual, Value = "ok" }
                }
            });
            Assert.True(result);
        }

        [Fact]
        public void Dictionary_SpecificCase()
        {
            var dict = new Dictionary<string, object>() {
                {"fa3cca71-6f90-440a-bad4-217f141cf20c", "da3cca71-6f90-440a-bad4-217f141cf20c" },
                {"1235", "nok" }
            };

            var result = JsonRuleEngine.Evaluate(dict, new ConditionRuleSet<bool>()
            {
                Separator = ConditionRuleSeparator.And,
                Rules = new[] {
                    new ConditionRuleSet<bool>() {
                        Field = "fa3cca71-6f90-440a-bad4-217f141cf20c",
                        Operator = ConditionRuleOperator.contains,
                        Value = "aaaa"
                    }
                }
            });
            Assert.False(result);
        }



        [Fact]
        public void DictionaryDeserialized()
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>("{\"Test\":\"1\"}");
            var conditions = JsonConvert.DeserializeObject<ConditionRuleSet>("{\"operator\":\"equal\",\"field\":\"Test\",\"value\":\"1\"}");

            bool result = JsonRuleEngine.Evaluate(
                dict,
                conditions
                );
            Assert.True(result); // Return true
        }





        [Fact]
        public void Dictionary_NoField()
        {
            var dict = new Dictionary<string, object>() {
                {"1235", "ok2" }
            };
            bool result = JsonRuleEngine.Evaluate(dict, new ConditionRuleSet<bool>() { Field = "1234", Operator = ConditionRuleOperator.isNotNull });
            Assert.False(result);
        }


        [Fact]
        public void Simple()
        {
            List<Game> list = Test("simple.json");

            Assert.True(list.Count == 1);
        }

        [Fact]
        public void SimpleReturn()
        {
            string rules = GetJsonTestFile("simpleReturn.json");
            var data = FakeGameService.GetDatas().First(x => x.Name == "GTA V");

            var returnValue = JsonRuleEngine.Evaluate<Game, string>(data, rules);

            Assert.True(returnValue == "ThisIsTheReturnValue");
        }

        [Fact]
        public void TryEvaluateTest()
        {
            string rules = GetJsonTestFile("simpleReturn.json");
            var data = FakeGameService.GetDatas().First(x => x.Name == "GTA V");

            var result = JsonRuleEngine.TryEvaluate<Game, string>(data, rules, out var returnValue);

            Assert.True(result);
            Assert.True(returnValue == "ThisIsTheReturnValue");
        }

        [Fact]
        public void ComplexReturn()
        {
            string rules = GetJsonTestFile("complexReturn.json");
            var expression = JsonRuleEngine.ParseExpression<Game>(rules);
            var datas = FakeGameService.GetDatas().Where(expression).ToList();

            foreach (var data in datas)
            {
                var returnValue = JsonRuleEngine.Evaluate<Game, Review>(data, rules);

                Assert.True(returnValue.Id == 99);
                Assert.True(returnValue.Text == "Defined in json");
                Assert.True(returnValue.Author.Name == "Athur Review");
            }
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
        public void Enum()
        {
            List<Game> list = Test("enum.json");
            var expectedCount = FakeGameService.GetDatas()
                .Count(m => m.Type == GameType.CityBuilder || m.Type == GameType.RPG);
            Assert.True(list.Count == expectedCount, $"{list.Count}/{expectedCount}");
        }



        [Fact]
        public void Complex()
        {
            List<Game> list = Test("complex.json");
            Assert.True(list.Count > 0);
        }

        [Fact]
        public void ListIsEmpty()
        {
            List<Game> list = Test("listIsEmpty.json");

            Assert.NotEmpty(list);
            Assert.True(list.All(m => !m.Reviews.Any()));
        }

        [Fact]
        public void ListIsNotEmpty()
        {
            List<Game> list = Test("listIsNotEmpty.json");

            Assert.NotEmpty(list);
            Assert.True(list.All(m => m.Reviews.Any()));
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
        public void DeepProps()
        {
            string rules = GetJsonTestFile("deepProps.json");
            var items = FakeGameService.GetDatas();
            var result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.True(result);
        }


        [Fact]
        public void TimeSpan()
        {
            string rules = GetJsonTestFile("timespan.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(new Game() { Date = DateTime.UtcNow }, rules);
            Assert.True(result);
        }

        [Fact]
        public void Object()
        {
            var dict = new Game()
            {
                Name = "Warzone"
            };
            bool result = JsonRuleEngine.Evaluate((object)dict, new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Warzone" });
            Assert.True(result);
        }


        [Fact]
        public void ObjectSubProperty()
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
        public void ListNotIn()
        {
            string rules = GetJsonTestFile("listNotIn.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.False(result);

        }

        [Fact]
        public void ListNotInAlt()
        {
            var rules = GetJsonTestFile("listNotInAlt.json");
            var result = JsonRuleEngine.Evaluate(new Game()
            {
                Name = "Toto",
                Reviews = new[]
                 {
                      new Review()
                     {
                         Id = 1
                     },
                     new Review()
                     {
                         Id = 2
                     }
                 }
            }, rules);

            Assert.False(result);
        }

        [Fact]
        public void NotEqualList()
        {
            var rules = GetJsonTestFile("notEqualList.json");
            var result = JsonRuleEngine.Evaluate(new Game()
            {
                Name = "Toto",
                Reviews = new[]
                 {
                      new Review()
                     {
                         Id = 1
                     },
                     new Review()
                     {
                         Id = 2
                     }
                 }
            }, rules);

            Assert.False(result);
        }


        [Fact]
        public void Empty()
        {
            string rules = GetJsonTestFile("empty.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), rules);
            Assert.False(result);
        }

        [Fact]
        public void IsNull()
        {
            string rules = GetJsonTestFile("isNull.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(new Game() { Category = null, Date = null }, rules);
            Assert.True(result);
        }

        [Fact]
        public void IsNotNull()
        {
            string rules = GetJsonTestFile("isNotNull.json");
            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(new Game() { Category = "Titi", Date = DateTime.UtcNow }, rules);
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

        [Fact]
        public void EvaluateWithClass()
        {
            string rules = GetJsonTestFile("complex.json");

            var items = FakeGameService.GetDatas();
            bool result = JsonRuleEngine.Evaluate(items.First(), new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.isNotNull });
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
