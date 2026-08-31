using JsonRuleEngine.Net.Models;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        /// <summary>
        /// The cache key derives from the rule CONTENT : mutating a rule object
        /// between two evaluations is always honored
        /// </summary>
        [Fact]
        public void CompiledCache_RuleMutation_IsHonored()
        {
            var engine = new JsonRuleEngine();
            var game = new Game() { Name = "Zelda" };
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Zelda" };

            Assert.True(engine.Evaluate(game, rule));

            rule.Value = "Mario";
            Assert.False(engine.Evaluate(game, rule));

            rule.Value = "Zelda";
            rule.Operator = ConditionRuleOperator.notEqual;
            Assert.False(engine.Evaluate(game, rule));
        }

        /// <summary>
        /// Repeated evaluations of the same rule stay correct (cache hit path)
        /// </summary>
        [Fact]
        public void CompiledCache_RepeatedEvaluate_IsCorrect()
        {
            var engine = new JsonRuleEngine();
            var rule = new ConditionRuleSet() { Field = "Price", Operator = ConditionRuleOperator.greaterThan, Value = 30 };
            var expected = FakeGameService.GetData().Count(m => m.Price > 30);

            for (var i = 0; i < 3; i++)
            {
                var count = FakeGameService.GetData().Count(m => engine.Evaluate(m, rule));
                Assert.Equal(expected, count);
            }
        }

        /// <summary>
        /// The same holds for the json string overload
        /// </summary>
        [Fact]
        public void CompiledCache_JsonString_IsCorrect()
        {
            var engine = new JsonRuleEngine();
            var json = "{\"field\":\"Name\",\"operator\":\"equal\",\"value\":\"Destiny\"}";

            Assert.True(engine.Evaluate(new Game() { Name = "Destiny" }, json));
            Assert.False(engine.Evaluate(new Game() { Name = "Zelda" }, json));
            Assert.True(engine.Evaluate(new Game() { Name = "Destiny" }, json));
        }

        /// <summary>
        /// An engine with a custom accessor never uses the cache : swapping the
        /// accessor between two evaluations of the same rule is honored
        /// </summary>
        [Fact]
        public void CompiledCache_BypassedWhenAccessorSet()
        {
            var game = new Game() { Name = "Zelda", Category = "Adventure" };
            var rule = new ConditionRuleSet() { Field = "Virtual", Operator = ConditionRuleOperator.equal, Value = "Zelda" };

            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
                ctx.Field == "Virtual"
                    ? ctx.ApplyOperator(Expression.Property(ctx.InputParam, nameof(Game.Name)))
                    : null;
            Assert.True(engine.Evaluate(game, rule));

            // Same engine, same rule content, different accessor : must recompile
            engine.CustomConditionRuleSetAccessor = (ctx) =>
                ctx.Field == "Virtual"
                    ? ctx.ApplyOperator(Expression.Property(ctx.InputParam, nameof(Game.Category)))
                    : null;
            Assert.False(engine.Evaluate(game, rule));
        }

        /// <summary>
        /// Same rule content on two different input types gives two distinct entries
        /// </summary>
        [Fact]
        public void CompiledCache_DistinctPerType()
        {
            var engine = new JsonRuleEngine();
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "x" };

            Assert.False(engine.Evaluate(new Game() { Name = "y" }, rule));
            Assert.True(engine.Evaluate(new Editor() { Name = "x" }, rule));
        }

        /// <summary>
        /// The cache can be disabled per engine instance
        /// </summary>
        [Fact]
        public void CompiledCache_CanBeDisabled()
        {
            var engine = new JsonRuleEngine() { UseCompiledExpressionCache = false };
            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Zelda" };

            Assert.True(engine.Evaluate(new Game() { Name = "Zelda" }, rule));
            Assert.False(engine.Evaluate(new Game() { Name = "Mario" }, rule));
        }
    }
}
