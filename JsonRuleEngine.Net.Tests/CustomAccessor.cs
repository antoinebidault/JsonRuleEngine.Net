using JsonRuleEngine.Net.Models;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        /// <summary>
        /// Fake of EF.Functions.JsonValue : reads a value in a json like store, as a string
        /// </summary>
        public static string JsonValue(Dictionary<string, object> json, string path)
        {
            object value;
            if (json != null && json.TryGetValue(path, out value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }

        /// <summary>
        /// Same as <see cref="JsonValue"/> but typed as int, to test the operators on value types
        /// </summary>
        public static int JsonValueInt(Dictionary<string, object> json, string path)
        {
            var value = JsonValue(json, path);
            int output;
            return int.TryParse(value, out output) ? output : 0;
        }

        /// <summary>
        /// Returns a nullable boolean, to test the bool? handling of the accessor
        /// </summary>
        public static bool? IsExpensive(Game game)
        {
            return game == null ? (bool?)null : game.Price > 40;
        }

        public static string Upper(string value)
        {
            return value == null ? null : value.ToUpperInvariant();
        }

        public static string DictionaryString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }

        private static readonly MethodInfo JsonValueMethod = typeof(BaseTests).GetMethod(nameof(JsonValue));
        private static readonly MethodInfo JsonValueIntMethod = typeof(BaseTests).GetMethod(nameof(JsonValueInt));
        private static readonly MethodInfo IsExpensiveMethod = typeof(BaseTests).GetMethod(nameof(IsExpensive));
        private static readonly MethodInfo UpperMethod = typeof(BaseTests).GetMethod(nameof(Upper));
        private static readonly MethodInfo DictionaryStringMethod = typeof(BaseTests).GetMethod(nameof(DictionaryString));

        /// <summary>
        /// Build the CustomFields.[path] access as a single method call,
        /// like EF.Functions.JsonValue(w.ExtraInformation, "$.path") would do
        /// </summary>
        private static Expression JsonAccess(ConditionRuleSetAccessorContext ctx, string path, MethodInfo method = null)
        {
            return Expression.Call(method ?? JsonValueMethod,
                Expression.Property(ctx.InputParam, nameof(Game.CustomFields)),
                Expression.Constant(path));
        }

        private static Game GameWithCustomFields()
        {
            return new Game()
            {
                Name = "Zelda",
                Price = 59.9,
                CustomFields = new Dictionary<string, object>()
                {
                    { "Tld", "fr" },
                    { "Score", 12 }
                }
            };
        }

        /// <summary>
        /// A field that does not exist on T can be claimed by the accessor :
        /// it bypasses the field validation and the collection regrouping pass
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_VirtualField()
        {
            var game = GameWithCustomFields();

            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field != null && ctx.Field.StartsWith("Json."))
                {
                    return ctx.ApplyOperator(JsonAccess(ctx, ctx.Field.Substring("Json.".Length)));
                }

                return null;
            };

            var rule = new ConditionRuleSet() { Field = "Json.Tld", Operator = ConditionRuleOperator.equal, Value = "fr" };

            Assert.True(engine.Evaluate(game, rule));
            Assert.False(engine.Evaluate(game, new ConditionRuleSet() { Field = "Json.Tld", Operator = ConditionRuleOperator.equal, Value = "de" }));

            // Without the accessor, the field does not exist and the engine rejects it
            Assert.Throws<JsonRuleEngineException>(() => new JsonRuleEngine().Evaluate(game, rule));
        }

        /// <summary>
        /// A claimed virtual field must also work when mixed with standard fields in a nested tree
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_VirtualFieldInsideTree()
        {
            var game = GameWithCustomFields();

            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field != null && ctx.Field.StartsWith("Json."))
                {
                    return ctx.ApplyOperator(JsonAccess(ctx, ctx.Field.Substring("Json.".Length)));
                }

                return null;
            };

            var rules = new ConditionRuleSet()
            {
                Separator = ConditionRuleSeparator.And,
                Rules = new List<ConditionRuleSet>()
                {
                    new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Zelda" },
                    new ConditionRuleSet()
                    {
                        Separator = ConditionRuleSeparator.Or,
                        Rules = new List<ConditionRuleSet>()
                        {
                            new ConditionRuleSet() { Field = "Json.Tld", Operator = ConditionRuleOperator.equal, Value = "de" },
                            new ConditionRuleSet() { Field = "Json.Tld", Operator = ConditionRuleOperator.equal, Value = "fr" }
                        }
                    }
                }
            };

            Assert.True(engine.Evaluate(game, rules));
        }

        /// <summary>
        /// A boolean expression returned by the accessor is used as is, without applying the operator
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_ReturnsBoolExpression()
        {
            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field == "EditorName")
                {
                    // Complete predicate, the value of the rule is ignored on purpose
                    return Expression.Equal(
                        Expression.Property(Expression.Property(ctx.InputParam, nameof(Game.Editor)), nameof(Editor.Name)),
                        Expression.Constant("Ubisoft"));
                }

                return null;
            };

            var rule = new ConditionRuleSet() { Field = "EditorName", Operator = ConditionRuleOperator.equal, Value = "ignored" };
            var games = FakeGameService.GetData().Where(engine.ParseExpression<Game>(rule)).ToList();

            Assert.Single(games);
            Assert.Equal("Assassin's creed", games[0].Name);
        }

        /// <summary>
        /// A nullable boolean expression is coerced to a predicate
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_ReturnsNullableBoolExpression()
        {
            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field == "IsExpensive")
                {
                    return Expression.Call(IsExpensiveMethod, ctx.InputParam);
                }

                return null;
            };

            var rule = new ConditionRuleSet() { Field = "IsExpensive" };
            var games = FakeGameService.GetData().Where(engine.ParseExpression<Game>(rule)).ToList();

            Assert.Equal(new[] { "Assassin's creed", "GTA V" }, games.Select(m => m.Name).ToArray());
        }

        /// <summary>
        /// A non boolean expression goes through the engine operator logic
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_AutoAppliesOperator()
        {
            var game = GameWithCustomFields();

            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field == "Json.Tld")
                {
                    return JsonAccess(ctx, "Tld");
                }

                if (ctx.Field == "Json.Score")
                {
                    return JsonAccess(ctx, "Score", JsonValueIntMethod);
                }

                return null;
            };

            // in
            Assert.True(engine.Evaluate(game, new ConditionRuleSet()
            {
                Field = "Json.Tld",
                Operator = ConditionRuleOperator.@in,
                Value = JArray.FromObject(new[] { "be", "fr" })
            }));

            Assert.False(engine.Evaluate(game, new ConditionRuleSet()
            {
                Field = "Json.Tld",
                Operator = ConditionRuleOperator.notIn,
                Value = JArray.FromObject(new[] { "be", "fr" })
            }));

            // contains
            Assert.True(engine.Evaluate(game, new ConditionRuleSet() { Field = "Json.Tld", Operator = ConditionRuleOperator.contains, Value = "f" }));

            // greaterThan on a value type, with a string value to convert
            Assert.True(engine.Evaluate(game, new ConditionRuleSet() { Field = "Json.Score", Operator = ConditionRuleOperator.greaterThan, Value = 3 }));
            Assert.False(engine.Evaluate(game, new ConditionRuleSet() { Field = "Json.Score", Operator = ConditionRuleOperator.lessThan, Value = "3" }));
        }

        /// <summary>
        /// Returning null must leave the engine behavior untouched
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_NullFallsThrough()
        {
            var rules = new ConditionRuleSet()
            {
                Separator = ConditionRuleSeparator.Or,
                Rules = new List<ConditionRuleSet>()
                {
                    new ConditionRuleSet() { Field = "Reviews.Text", Operator = ConditionRuleOperator.contains, Value = "very" },
                    new ConditionRuleSet() { Field = "Tags", Operator = ConditionRuleOperator.@in, Value = JArray.FromObject(new[] { "Survival" }) },
                    new ConditionRuleSet()
                    {
                        Separator = ConditionRuleSeparator.And,
                        Rules = new List<ConditionRuleSet>()
                        {
                            new ConditionRuleSet() { Field = "Editor.Name", Operator = ConditionRuleOperator.equal, Value = "test" },
                            new ConditionRuleSet() { Field = "Price", Operator = ConditionRuleOperator.greaterThan, Value = 50 }
                        }
                    }
                }
            };

            var expected = FakeGameService.GetData().Where(new JsonRuleEngine().ParseExpression<Game>(rules)).Select(m => m.Name).ToArray();

            int calls = 0;
            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                calls++;
                return null;
            };

            var actual = FakeGameService.GetData().Where(engine.ParseExpression<Game>(rules)).Select(m => m.Name).ToArray();

            Assert.Equal(expected, actual);
            Assert.True(calls > 0);
        }

        /// <summary>
        /// A sub rule of a collection is passed to the accessor with the collection item parameter,
        /// and the engine still wraps the result in a Any() call
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_InsideCollectionRules()
        {
            bool collectionItemSeen = false;

            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.IsCollectionItem && ctx.Field == "Text" && ctx.InputType == typeof(Review))
                {
                    collectionItemSeen = true;
                    return ctx.ApplyOperator(Expression.Call(UpperMethod, Expression.Property(ctx.InputParam, nameof(Review.Text))));
                }

                return null;
            };

            var rule = new ConditionRuleSet() { Field = "Reviews.Text", Operator = ConditionRuleOperator.equal, Value = "IT'S VERY COOL" };
            var games = FakeGameService.GetData().Where(engine.ParseExpression<Game>(rule)).ToList();

            Assert.True(collectionItemSeen);
            Assert.Single(games);
            Assert.Equal("Assassin's creed", games[0].Name);
        }

        /// <summary>
        /// The accessor has priority over the EvaluateOptions transformers
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_TakesPrecedenceOverTransformer()
        {
            var options = new EvaluateOptions<Game>()
                .ForProperty("Alias", g => g.Name);

            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field == "Alias")
                {
                    // The accessor points on the editor name instead of the game name
                    return ctx.ApplyOperator(Expression.Property(Expression.Property(ctx.InputParam, nameof(Game.Editor)), nameof(Editor.Name)));
                }

                return null;
            };

            var rule = new ConditionRuleSet() { Field = "Alias", Operator = ConditionRuleOperator.equal, Value = "Ubisoft" };

            var withAccessor = FakeGameService.GetData().Where(engine.ParseExpression<Game>(rule, options)).Select(m => m.Name).ToList();
            Assert.Equal(new[] { "Assassin's creed" }, withAccessor);

            // Without the accessor, the transformer is used and nothing matches
            var withTransformer = FakeGameService.GetData().Where(new JsonRuleEngine().ParseExpression<Game>(rule, options)).ToList();
            Assert.Empty(withTransformer);
        }

        /// <summary>
        /// The accessor can rewrite the compared value
        /// </summary>
        [Fact]
        public void CustomConditionRuleSetAccessor_ValueRewrite()
        {
            var engine = new JsonRuleEngine();
            engine.CustomConditionRuleSetAccessor = (ctx) =>
            {
                if (ctx.Field == "Name")
                {
                    ctx.Value = "Destiny";
                    return Expression.Property(ctx.InputParam, nameof(Game.Name));
                }

                return null;
            };

            var rule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Zelda" };
            var games = FakeGameService.GetData().Where(engine.ParseExpression<Game>(rule)).ToList();

            Assert.Single(games);
            Assert.Equal("Destiny", games[0].Name);
        }

        /// <summary>
        /// The per segment accessor keeps working : here it handles the dictionary access itself
        /// </summary>
        [Fact]
        public void CustomPropertyAccessor_Dictionary()
        {
            var game = GameWithCustomFields();

            bool called = false;
            var engine = new JsonRuleEngine();
            engine.CustomPropertyAccessor = (ctx) =>
            {
                var exp = ctx.Expression;
                if (exp != null && exp.Type == typeof(Dictionary<string, object>))
                {
                    called = true;
                    return Expression.Call(DictionaryStringMethod, exp, Expression.Constant(ctx.MemberName));
                }

                return null;
            };

            Assert.True(engine.Evaluate(game, new ConditionRuleSet() { Field = "CustomFields.Tld", Operator = ConditionRuleOperator.equal, Value = "fr" }));
            Assert.True(called);

            Assert.False(engine.Evaluate(game, new ConditionRuleSet() { Field = "CustomFields.Tld", Operator = ConditionRuleOperator.equal, Value = "de" }));
        }
    }
}
