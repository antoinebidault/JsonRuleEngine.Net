using JsonRuleEngine.Net.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    /// <summary>
    /// The corpus is the executable specification of the engine :
    /// each case pairs a rule with the equivalent LINQ predicate.
    /// It pins the engine semantics against any compiler change (golden master).
    /// </summary>
    public static class RuleCorpus
    {
        public class GameCase
        {
            public string Name { get; set; }
            public ConditionRuleSet Rule { get; set; }
            public Func<Game, bool> Expected { get; set; }
            public EvaluateOptions<Game> Options { get; set; }
        }

        public class DictCase
        {
            public string Name { get; set; }
            public ConditionRuleSet Rule { get; set; }
            public Func<Dictionary<string, object>, bool> Expected { get; set; }
        }

        public static readonly Guid KnownGuid = new Guid("6b2b39ab-6d3b-4c22-b1f2-e57d3ffe5810");

        /// <summary>
        /// The fake game data, plus edge case objects (null navigation, null tags, dictionaries...)
        /// </summary>
        public static List<Game> Games()
        {
            var games = FakeGameService.GetData().ToList();

            games.Add(new Game()
            {
                Id = KnownGuid,
                Name = "EdgeCase",
                Price = 5,
                Stock = null,
                State = GameState.Removed,
                BoolValue = false,
                Date = null,
                DateCreation = new DateTime(2020, 6, 15),
                Editor = null,
                Tags = null,
                CustomFields = null,
                Reviews = new List<Review>()
                {
                    new Review() { Id = 9, Text = null, Author = null }
                }
            });

            games.Add(new Game()
            {
                Name = "DictHolder",
                Price = 30,
                Stock = 4,
                Date = new DateTime(2021, 6, 1),
                Tags = new[] { "RPG", "Survival" },
                CustomFields = new Dictionary<string, object>()
                {
                    { "n", 12 },
                    { "d", 4.5 },
                    { "s", "hello world" },
                    { "b", true },
                    { "nil", null },
                    { "list", new List<object>() { "a", "b" } }
                },
                Reviews = new List<Review>()
            });

            return games;
        }

        private static ConditionRuleSet L(string field, ConditionRuleOperator op, object value = null)
        {
            return new ConditionRuleSet() { Field = field, Operator = op, Value = value };
        }

        private static ConditionRuleSet G(ConditionRuleSeparator sep, params ConditionRuleSet[] rules)
        {
            return new ConditionRuleSet() { Separator = sep, Rules = rules.ToList() };
        }

        public static List<GameCase> GameCases()
        {
            var cases = new List<GameCase>();
            void Add(string name, ConditionRuleSet rule, Func<Game, bool> expected, EvaluateOptions<Game> options = null)
                => cases.Add(new GameCase() { Name = name, Rule = rule, Expected = expected, Options = options });

            // ---- Simple operators on scalar properties ----
            Add("equal_string", L("Name", ConditionRuleOperator.equal, "GTA V"),
                g => g.Name == "GTA V");
            Add("equal_null_value", L("Category", ConditionRuleOperator.equal, null),
                g => g.Category == null);
            Add("notEqual_string", L("Category", ConditionRuleOperator.notEqual, "Action"),
                g => g.Category != "Action");
            Add("greaterThan_double", L("Price", ConditionRuleOperator.greaterThan, 30),
                g => g.Price > 30);
            Add("greaterThanInclusive_double", L("Price", ConditionRuleOperator.greaterThanInclusive, 24),
                g => g.Price >= 24);
            Add("lessThan_double", L("Price", ConditionRuleOperator.lessThan, 23),
                g => g.Price < 23);
            Add("lessThanInclusive_double", L("Price", ConditionRuleOperator.lessThanInclusive, 23.3),
                g => g.Price <= 23.3);
            Add("contains_string", L("Name", ConditionRuleOperator.contains, "GTA"),
                g => g.Name != null && g.Name.Contains("GTA"));
            Add("doesNotContains_string", L("Name", ConditionRuleOperator.doesNotContains, "GTA"),
                g => g.Name != null && !g.Name.Contains("GTA"));
            Add("regex_string", L("Name", ConditionRuleOperator.regex, "^GTA [IV]+$"),
                g => g.Name != null && System.Text.RegularExpressions.Regex.IsMatch(g.Name, "^GTA [IV]+$"));
            Add("regex_collection", L("Reviews.Text", ConditionRuleOperator.regex, "(very ){2,}"),
                g => g.Reviews != null && g.Reviews.Any(r => r.Text != null && System.Text.RegularExpressions.Regex.IsMatch(r.Text, "(very ){2,}")));
            Add("regex_primitiveCollection", L("Tags", ConditionRuleOperator.regex, "^Surv"),
                g => g.Tags != null && g.Tags.Any(t => t != null && System.Text.RegularExpressions.Regex.IsMatch(t, "^Surv")));
            Add("regex_dictProp", L("CustomFields.s", ConditionRuleOperator.regex, "wor.d$"),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("s") && g.CustomFields["s"] is string s
                     && System.Text.RegularExpressions.Regex.IsMatch(s, "wor.d$"));
            Add("in_jarray_string", L("Name", ConditionRuleOperator.@in, JArray.FromObject(new[] { "Destiny", "Sim City" })),
                g => new[] { "Destiny", "Sim City" }.Contains(g.Name));
            Add("notIn_jarray_string", L("Name", ConditionRuleOperator.notIn, JArray.FromObject(new[] { "Destiny", "Sim City" })),
                g => !new[] { "Destiny", "Sim City" }.Contains(g.Name));
            Add("in_list_string", L("Name", ConditionRuleOperator.@in, new List<string>() { "The forest" }),
                g => g.Name == "The forest");
            Add("in_jarray_double", L("Price", ConditionRuleOperator.@in, JArray.FromObject(new object[] { 45.3, 13 })),
                g => new[] { 45.3, 13d }.Contains(g.Price));
            Add("bool_equal", L("BoolValue", ConditionRuleOperator.equal, true),
                g => g.BoolValue);
            Add("guid_equal", L("Id", ConditionRuleOperator.equal, KnownGuid.ToString()),
                g => g.Id == KnownGuid);

            // ---- Nullable properties ----
            Add("nullable_isNull", L("Stock", ConditionRuleOperator.isNull),
                g => !g.Stock.HasValue);
            Add("nullable_isNotNull", L("Stock", ConditionRuleOperator.isNotNull),
                g => g.Stock.HasValue);
            Add("nullable_greaterThan", L("Stock", ConditionRuleOperator.greaterThan, 0),
                g => g.Stock.HasValue && g.Stock > 0);
            Add("nullable_equal", L("Stock", ConditionRuleOperator.equal, 1),
                g => g.Stock.HasValue && g.Stock == 1);
            Add("nullable_date_isNull", L("Date", ConditionRuleOperator.isNull),
                g => !g.Date.HasValue);

            // ---- Enums ----
            Add("enum_equal_name", L("Type", ConditionRuleOperator.equal, "RPG"),
                g => g.Type == GameType.RPG);
            Add("enum_equal_numericString", L("Type", ConditionRuleOperator.equal, "1"),
                g => g.Type == GameType.RPG);
            Add("nullable_enum_equal", L("State", ConditionRuleOperator.equal, "New"),
                g => g.State.HasValue && g.State == GameState.New);

            // ---- Dates ----
            Add("date_equal_dateOnlyFormat", L("Date", ConditionRuleOperator.equal, "2018-01-01"),
                g => g.Date.HasValue && g.Date.Value.Date == new DateTime(2018, 1, 1));
            Add("date_greaterThan_dateOnlyFormat", L("Date", ConditionRuleOperator.greaterThan, "2021-01-05"),
                g => g.Date.HasValue && g.Date.Value.Date > new DateTime(2021, 1, 5));
            // Note : a full ISO date STRING is not supported by the engine, but Newtonsoft
            // deserializes json date strings to DateTime values, which is the real input
            Add("date_equal_dateTimeValue", L("Date", ConditionRuleOperator.equal, new DateTime(2021, 1, 1)),
                g => g.Date.HasValue && g.Date == new DateTime(2021, 1, 1));
            Add("date_notNullable_lessThan", L("DateCreation", ConditionRuleOperator.lessThan, "2021-01-10"),
                g => g.DateCreation.Date < new DateTime(2021, 1, 10));
            // Relative timespan value : all data dates are far away from the boundary (now +/- 10 years)
            Add("date_timespan_greaterThanInclusive", L("Date", ConditionRuleOperator.greaterThanInclusive, "\"-3650.10:00:00\""),
                g => g.Date.HasValue && g.Date.Value >= DateTime.UtcNow.AddDays(-3650).AddHours(-10));
            Add("date_timespan_period_lessThan", L("Date", ConditionRuleOperator.lessThan, "\"3650.00:00:00\""),
                g => g.Date.HasValue && g.Date.Value.Date < DateTime.UtcNow.AddDays(3650).Date);

            // ---- Navigation properties ----
            Add("nav_equal", L("Editor.Name", ConditionRuleOperator.equal, "Ubisoft"),
                g => g.Editor != null && g.Editor.Name == "Ubisoft");
            Add("nav_isNull_withNullParent", L("Editor.Name", ConditionRuleOperator.isNull),
                g => g.Editor == null || g.Editor.Name == null);
            Add("nav_deep_isNotNull", L("Editor.Company.Name", ConditionRuleOperator.isNotNull),
                g => g.Editor != null && g.Editor.Company != null && g.Editor.Company.Name != null);

            // ---- Collections of classes ----
            Add("collection_equal", L("Reviews.Text", ConditionRuleOperator.equal, "It's cool"),
                g => g.Reviews != null && g.Reviews.Any(r => r.Text == "It's cool"));
            Add("collection_in", L("Reviews.Id", ConditionRuleOperator.@in, JArray.FromObject(new[] { 1, 3 })),
                g => g.Reviews != null && g.Reviews.Any(r => new[] { 1, 3 }.Contains(r.Id)));
            Add("collection_notEqual_isAll", L("Reviews.Id", ConditionRuleOperator.notEqual, 1),
                g => g.Reviews == null || g.Reviews.All(r => r.Id != 1));
            Add("collection_notIn_isAll", L("Reviews.Id", ConditionRuleOperator.notIn, JArray.FromObject(new[] { 1, 2 })),
                g => g.Reviews == null || g.Reviews.All(r => !new[] { 1, 2 }.Contains(r.Id)));
            Add("collection_contains", L("Reviews.Text", ConditionRuleOperator.contains, "very"),
                g => g.Reviews != null && g.Reviews.Any(r => r.Text != null && r.Text.Contains("very")));
            Add("collection_isEmpty", L("Reviews", ConditionRuleOperator.isEmpty),
                g => g.Reviews == null || !g.Reviews.Any());
            Add("collection_isNotEmpty", L("Reviews", ConditionRuleOperator.isNotEmpty),
                g => g.Reviews != null && g.Reviews.Any());
            Add("collection_includeAll", L("Reviews.Id", ConditionRuleOperator.includeAll, JArray.FromObject(new[] { "2", "1" })),
                g => g.Reviews != null && g.Reviews.Any(r => r.Id == 2) && g.Reviews.Any(r => r.Id == 1));
            Add("collection_excludeAll", L("Reviews.Id", ConditionRuleOperator.excludeAll, JArray.FromObject(new[] { "2", "1" })),
                g => g.Reviews != null && !g.Reviews.Any(r => r.Id == 2) && !g.Reviews.Any(r => r.Id == 1));

            // Sibling rules on the same collection are merged in a single Any
            // (same element must match all conditions when the separator is And)
            Add("collection_siblingMerge_and",
                G(ConditionRuleSeparator.And,
                    L("Reviews.Id", ConditionRuleOperator.equal, 1),
                    L("Reviews.Text", ConditionRuleOperator.equal, "It's very cool")),
                g => g.Reviews != null && g.Reviews.Any(r => r.Id == 1 && r.Text == "It's very cool"));
            Add("collection_siblingMerge_or",
                G(ConditionRuleSeparator.Or,
                    L("Reviews.Id", ConditionRuleOperator.equal, 3),
                    L("Reviews.Text", ConditionRuleOperator.equal, "It's cool")),
                g => g.Reviews != null && g.Reviews.Any(r => r.Id == 3 || r.Text == "It's cool"));
            // isNotEmpty combined with another rule on the same collection : v1 drops the isNotEmpty part
            Add("collection_isNotEmpty_combined",
                G(ConditionRuleSeparator.And,
                    L("Reviews", ConditionRuleOperator.isNotEmpty),
                    L("Reviews.Text", ConditionRuleOperator.equal, "It's cool")),
                g => g.Reviews != null && g.Reviews.Any(r => r.Text == "It's cool"));

            // ---- Nested collections ----
            Add("collection_deep_nested", L("Reviews.Author.Types.Name", ConditionRuleOperator.equal, "Reviewer"),
                g => g.Reviews != null && g.Reviews.Any(r =>
                        r.Author != null && r.Author.Types != null && r.Author.Types.Any(t => t.Name == "Reviewer")));

            // ---- Collections of primitives ----
            Add("tags_equal", L("Tags", ConditionRuleOperator.equal, "RPG"),
                g => g.Tags != null && g.Tags.Any(t => t == "RPG"));
            Add("tags_in", L("Tags", ConditionRuleOperator.@in, JArray.FromObject(new[] { "Survival", "Puzzle" })),
                g => g.Tags != null && g.Tags.Any(t => new[] { "Survival", "Puzzle" }.Contains(t)));
            Add("tags_notEqual_isAll", L("Tags", ConditionRuleOperator.notEqual, "RPG"),
                g => g.Tags == null || g.Tags.All(t => t != "RPG"));

            // ---- Groups ----
            Add("group_and",
                G(ConditionRuleSeparator.And,
                    L("Type", ConditionRuleOperator.equal, "Action"),
                    L("Price", ConditionRuleOperator.greaterThan, 50)),
                g => g.Type == GameType.Action && g.Price > 50);
            Add("group_or",
                G(ConditionRuleSeparator.Or,
                    L("Name", ConditionRuleOperator.equal, "Sim City"),
                    L("Name", ConditionRuleOperator.equal, "Destiny")),
                g => g.Name == "Sim City" || g.Name == "Destiny");
            Add("group_nested",
                G(ConditionRuleSeparator.And,
                    L("Price", ConditionRuleOperator.greaterThan, 10),
                    G(ConditionRuleSeparator.Or,
                        L("Type", ConditionRuleOperator.equal, "RPG"),
                        G(ConditionRuleSeparator.And,
                            L("Type", ConditionRuleOperator.equal, "Action"),
                            L("Name", ConditionRuleOperator.contains, "IV")))),
                g => g.Price > 10 && (g.Type == GameType.RPG || (g.Type == GameType.Action && g.Name != null && g.Name.Contains("IV"))));
            Add("group_mixed_scalar_and_collection",
                G(ConditionRuleSeparator.And,
                    L("Stock", ConditionRuleOperator.equal, 1),
                    L("Reviews.Id", ConditionRuleOperator.equal, 1),
                    L("Reviews.Text", ConditionRuleOperator.equal, "It's cool")),
                g => g.Stock.HasValue && g.Stock == 1 &&
                     g.Reviews != null && g.Reviews.Any(r => r.Id == 1 && r.Text == "It's cool"));
            Add("group_or_collections",
                G(ConditionRuleSeparator.Or,
                    L("Reviews.Id", ConditionRuleOperator.equal, 3),
                    L("Tags", ConditionRuleOperator.equal, "Survival")),
                g => (g.Reviews != null && g.Reviews.Any(r => r.Id == 3)) ||
                     (g.Tags != null && g.Tags.Any(t => t == "Survival")));

            // ---- Root leaf (no Rules) and empty rule ----
            Add("root_leaf_with_separator",
                new ConditionRuleSet() { Separator = ConditionRuleSeparator.And, Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Destiny" },
                g => g.Name == "Destiny");
            Add("root_empty", new ConditionRuleSet(),
                g => false);

            // ---- Dictionary property (Dictionary<string, object>) ----
            Add("dictProp_equal_string", L("CustomFields.s", ConditionRuleOperator.equal, "hello world"),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("s") && (string)g.CustomFields["s"] == "hello world");
            Add("dictProp_contains", L("CustomFields.s", ConditionRuleOperator.contains, "world"),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("s") && ((string)g.CustomFields["s"]).Contains("world"));
            Add("dictProp_greaterThan_int", L("CustomFields.n", ConditionRuleOperator.greaterThan, 5),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("n") && Convert.ToDouble(g.CustomFields["n"]) > 5d);
            Add("dictProp_lessThan_double", L("CustomFields.d", ConditionRuleOperator.lessThan, 5.5),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("d") && Convert.ToDouble(g.CustomFields["d"]) < 5.5);
            Add("dictProp_equal_bool", L("CustomFields.b", ConditionRuleOperator.equal, true),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("b") && (bool)g.CustomFields["b"]);
            // isNull propagates through null parents (OrElse guard on each segment)
            Add("dictProp_isNull", L("CustomFields.nil", ConditionRuleOperator.isNull),
                g => g.CustomFields == null || (g.CustomFields.ContainsKey("nil") && g.CustomFields["nil"] == null));
            Add("dictProp_isNotNull", L("CustomFields.s", ConditionRuleOperator.isNotNull),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("s") && g.CustomFields["s"] != null);
            Add("dictProp_missingKey_false", L("CustomFields.doesNotExist", ConditionRuleOperator.equal, "x"),
                g => false);
            // in / notIn on a scalar dictionary value : numbers compare as double whatever their type
            Add("dictProp_in_scalarNumber", L("CustomFields.n", ConditionRuleOperator.@in, JArray.FromObject(new[] { 5, 12 })),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("n")
                     && g.CustomFields["n"] != null && new[] { 5d, 12d }.Contains(Convert.ToDouble(g.CustomFields["n"])));
            Add("dictProp_notIn_scalarNumber", L("CustomFields.n", ConditionRuleOperator.notIn, JArray.FromObject(new[] { 5, 99 })),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("n")
                     && !(g.CustomFields["n"] != null && new[] { 5d, 99d }.Contains(Convert.ToDouble(g.CustomFields["n"]))));
            Add("dictProp_in_scalarString", L("CustomFields.s", ConditionRuleOperator.@in, JArray.FromObject(new[] { "hello world", "other" })),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("s")
                     && g.CustomFields["s"] != null && new[] { "hello world", "other" }.Contains(g.CustomFields["s"] as string));
            Add("dictProp_equal_onListValue", L("CustomFields.list", ConditionRuleOperator.equal, "a"),
                g => g.CustomFields != null && g.CustomFields.ContainsKey("list") &&
                     g.CustomFields["list"] is IEnumerable<object> e && e.Contains("a"));

            // ---- EvaluateOptions transformers ----
            // Transformer bodies are evaluated as-is : they must be null safe themselves
            var options = new EvaluateOptions<Game>()
                .ForProperty("EditorName", g => g.Editor != null ? g.Editor.Name : null)
                .ForProperty("ReviewCount", g => g.Reviews != null ? g.Reviews.Count() : 0);
            Add("transformer_equal", L("EditorName", ConditionRuleOperator.equal, "Ubisoft"),
                g => g.Editor != null && g.Editor.Name == "Ubisoft", options);
            Add("transformer_computed_greaterThan", L("ReviewCount", ConditionRuleOperator.greaterThan, 1),
                g => (g.Reviews != null ? g.Reviews.Count() : 0) > 1, options);

            return cases;
        }

        /// <summary>
        /// Rules evaluated against a root Dictionary&lt;string, object&gt;
        /// </summary>
        public static List<DictCase> DictionaryCases()
        {
            var cases = new List<DictCase>();
            void Add(string name, ConditionRuleSet rule, Func<Dictionary<string, object>, bool> expected)
                => cases.Add(new DictCase() { Name = name, Rule = rule, Expected = expected });

            Add("dictRoot_equal", L("key", ConditionRuleOperator.equal, "ok"),
                d => d.ContainsKey("key") && (string)d["key"] == "ok");
            Add("dictRoot_notEqual", L("key", ConditionRuleOperator.notEqual, "nope"),
                d => d.ContainsKey("key") && (string)d["key"] != "nope");
            Add("dictRoot_missing_isNotNull", L("missing", ConditionRuleOperator.isNotNull),
                d => d.ContainsKey("missing") && d["missing"] != null);
            Add("dictRoot_greaterThan_number", L("num", ConditionRuleOperator.greaterThan, 10),
                d => d.ContainsKey("num") && Convert.ToDouble(d["num"]) > 10d);
            Add("dictRoot_in_onListValue", L("list", ConditionRuleOperator.@in, new List<string>() { "1" }),
                d => d.ContainsKey("list") && d["list"] is IEnumerable<object> e && e.Cast<object>().Contains("1"));
            Add("dictRoot_in_scalarNumber", L("num", ConditionRuleOperator.@in, JArray.FromObject(new[] { 125, 12 })),
                d => d.ContainsKey("num") && d["num"] != null && new[] { 125d, 12d }.Contains(Convert.ToDouble(d["num"])));
            Add("dictRoot_notIn_scalarString", L("key", ConditionRuleOperator.notIn, JArray.FromObject(new[] { "nope", "other" })),
                d => d.ContainsKey("key") && !new[] { "nope", "other" }.Contains(d["key"] as string));
            Add("dictRoot_group",
                G(ConditionRuleSeparator.And,
                    L("key", ConditionRuleOperator.equal, "ok"),
                    L("num", ConditionRuleOperator.lessThanInclusive, 12)),
                d => d.ContainsKey("key") && (string)d["key"] == "ok" &&
                     d.ContainsKey("num") && Convert.ToDouble(d["num"]) <= 12d);

            return cases;
        }

        public static List<Dictionary<string, object>> Dictionaries()
        {
            return new List<Dictionary<string, object>>()
            {
                new Dictionary<string, object>() { { "key", "ok" }, { "num", 12 }, { "list", new List<object>() { "1", "2" } } },
                new Dictionary<string, object>() { { "key", "nope" }, { "num", 5 } },
                new Dictionary<string, object>() { { "missing", null } },
                new Dictionary<string, object>()
            };
        }
    }

    public partial class BaseTests
    {
        /// <summary>
        /// Golden master : the engine must produce exactly the results of the LINQ predicates
        /// on every object of the corpus data set
        /// </summary>
        [Fact]
        public void Characterization_GameCorpus()
        {
            var games = RuleCorpus.Games();
            var failures = new List<string>();

            foreach (var testCase in RuleCorpus.GameCases())
            {
                Func<Game, bool> predicate;
                try
                {
                    predicate = new JsonRuleEngine().ParseExpression<Game>(testCase.Rule, testCase.Options).Compile();
                }
                catch (Exception e)
                {
                    failures.Add($"{testCase.Name} : parse failed : {e.Message}");
                    continue;
                }

                foreach (var game in games)
                {
                    bool actual = predicate(game);
                    bool expected = testCase.Expected(game);
                    if (actual != expected)
                    {
                        failures.Add($"{testCase.Name} / {game.Name} : expected {expected}, got {actual}");
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void Characterization_DictionaryCorpus()
        {
            var dictionaries = RuleCorpus.Dictionaries();
            var failures = new List<string>();

            foreach (var testCase in RuleCorpus.DictionaryCases())
            {
                var predicate = new JsonRuleEngine().ParseExpression<Dictionary<string, object>>(testCase.Rule).Compile();

                for (var i = 0; i < dictionaries.Count; i++)
                {
                    bool actual = predicate(dictionaries[i]);
                    bool expected = testCase.Expected(dictionaries[i]);
                    if (actual != expected)
                    {
                        failures.Add($"{testCase.Name} / dict #{i} : expected {expected}, got {actual}");
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
