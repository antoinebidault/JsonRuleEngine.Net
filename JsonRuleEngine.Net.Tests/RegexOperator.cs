using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        [Fact]
        public void Regex_Match()
        {
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.regex, Value = "^GTA [IV]+$" };
            var games = FakeGameService.GetData().Where(new JsonRuleEngine().ParseExpression<Game>(rule)).ToList();

            Assert.Equal(new[] { "GTA V", "GTA IV" }, games.Select(m => m.Name).ToArray());
        }

        [Fact]
        public void Regex_NoMatch()
        {
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.regex, Value = "^\\d+$" };

            Assert.False(new JsonRuleEngine().Evaluate(new Game() { Name = "Zelda" }, rule));
        }

        [Fact]
        public void Regex_NullValue_NeverMatches()
        {
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.regex, Value = ".*" };

            Assert.False(new JsonRuleEngine().Evaluate(new Game() { Name = null }, rule));
        }

        [Fact]
        public void Regex_CaseSensitive()
        {
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.regex, Value = "^gta" };

            Assert.False(new JsonRuleEngine().Evaluate(new Game() { Name = "GTA V" }, rule));

            // Inline case insensitive flag
            rule.Value = "(?i)^gta";
            Assert.True(new JsonRuleEngine().Evaluate(new Game() { Name = "GTA V" }, rule));
        }

        [Fact]
        public void Regex_FromJsonString()
        {
            var json = "{\"field\":\"Name\",\"operator\":\"regex\",\"value\":\"^GTA\"}";

            Assert.True(new JsonRuleEngine().Evaluate(new Game() { Name = "GTA V" }, json));
            Assert.False(new JsonRuleEngine().Evaluate(new Game() { Name = "Zelda" }, json));
        }

        [Fact]
        public void Regex_NavigationProperty()
        {
            var rule = new ConditionRuleSet() { Field = "Editor.Name", Operator = ConditionRuleOperator.regex, Value = "soft$" };

            Assert.True(new JsonRuleEngine().Evaluate(new Game() { Editor = new Editor() { Name = "Ubisoft" } }, rule));
            Assert.False(new JsonRuleEngine().Evaluate(new Game() { Editor = null }, rule));
        }

        [Fact]
        public void Regex_Collection()
        {
            var rule = new ConditionRuleSet() { Field = "Reviews.Text", Operator = ConditionRuleOperator.regex, Value = "^It's (very )+cool$" };
            var games = FakeGameService.GetData().Where(new JsonRuleEngine().ParseExpression<Game>(rule)).ToList();
            var expected = FakeGameService.GetData()
                .Count(m => m.Reviews != null && m.Reviews.Any(r => r.Text != null && System.Text.RegularExpressions.Regex.IsMatch(r.Text, "^It's (very )+cool$")));

            Assert.Equal(expected, games.Count);
            Assert.True(games.Count > 0);
        }

        [Fact]
        public void Regex_Dictionary()
        {
            var game = new Game()
            {
                CustomFields = new Dictionary<string, object>()
                {
                    { "email", "test@dastra.eu" },
                    { "number", 42 }
                }
            };

            var rule = new ConditionRuleSet() { Field = "CustomFields.email", Operator = ConditionRuleOperator.regex, Value = "^[^@]+@[^@]+\\.[a-z]+$" };
            Assert.True(new JsonRuleEngine().Evaluate(game, rule));

            rule.Value = "@gmail\\.com$";
            Assert.False(new JsonRuleEngine().Evaluate(game, rule));

            // A non string dictionary value never matches
            var numberRule = new ConditionRuleSet() { Field = "CustomFields.number", Operator = ConditionRuleOperator.regex, Value = "\\d+" };
            Assert.False(new JsonRuleEngine().Evaluate(game, numberRule));
        }

        [Fact]
        public void Regex_InvalidPattern_Throws()
        {
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.regex, Value = "([unclosed" };

            var exception = Assert.Throws<JsonRuleEngineException>(() => new JsonRuleEngine().ParseExpression<Game>(rule));
            Assert.Equal(JsonRuleEngineExceptionCategory.InvalidValue, exception.Type);
        }

        [Fact]
        public void Regex_EmptyPattern_Throws()
        {
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.regex, Value = null };

            var exception = Assert.Throws<JsonRuleEngineException>(() => new JsonRuleEngine().ParseExpression<Game>(rule));
            Assert.Equal(JsonRuleEngineExceptionCategory.InvalidValue, exception.Type);
        }

        [Fact]
        public void Regex_NonStringField_Throws()
        {
            var rule = new ConditionRuleSet() { Field = "Price", Operator = ConditionRuleOperator.regex, Value = "\\d+" };

            var exception = Assert.Throws<JsonRuleEngineException>(() => new JsonRuleEngine().ParseExpression<Game>(rule));
            Assert.Equal(JsonRuleEngineExceptionCategory.InvalidValue, exception.Type);
        }
    }
}
