using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using JsonRuleEngine.Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonRuleEngine.Net.Benchmarks
{
    public class Game
    {
        public string Name { get; set; }
        public double Price { get; set; }
        public int? Stock { get; set; }
        public string Category { get; set; }
        public DateTime? Date { get; set; }
        public Editor Editor { get; set; }
        public ICollection<Review> Reviews { get; set; }
    }

    public class Editor
    {
        public string Name { get; set; }
    }

    public class Review
    {
        public int Id { get; set; }
        public string Text { get; set; }
    }

    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 2, iterationCount: 6)]
    public class EngineBenchmarks
    {
        private ConditionRuleSet _simpleRule;
        private ConditionRuleSet _complexRule;
        private string _complexJson;
        private Game _game;

        private JsonRuleEngine _uncachedParse;
        private JsonRuleEngine _cached;
        private JsonRuleEngine _uncached;

        [GlobalSetup]
        public void Setup()
        {
            _simpleRule = new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "GTA V" };

            _complexRule = new ConditionRuleSet()
            {
                Separator = ConditionRuleSeparator.And,
                Rules = new List<ConditionRuleSet>()
                {
                    new ConditionRuleSet() { Field = "Editor.Name", Operator = ConditionRuleOperator.equal, Value = "Ubisoft" },
                    new ConditionRuleSet() { Field = "Reviews.Id", Operator = ConditionRuleOperator.@in, Value = new List<int>() { 1, 2 } },
                    new ConditionRuleSet() { Field = "Reviews.Text", Operator = ConditionRuleOperator.contains, Value = "cool" },
                    new ConditionRuleSet() { Field = "Price", Operator = ConditionRuleOperator.greaterThan, Value = 5 },
                    new ConditionRuleSet()
                    {
                        Separator = ConditionRuleSeparator.Or,
                        Rules = new List<ConditionRuleSet>()
                        {
                            new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "Assassin's creed" },
                            new ConditionRuleSet() { Field = "Category", Operator = ConditionRuleOperator.equal, Value = "Adventure" },
                            new ConditionRuleSet() { Field = "Stock", Operator = ConditionRuleOperator.greaterThan, Value = 0 }
                        }
                    }
                }
            };

            _complexJson = Newtonsoft.Json.JsonConvert.SerializeObject(_complexRule);

            _game = new Game()
            {
                Name = "Assassin's creed",
                Price = 45.3,
                Stock = 1,
                Category = "Adventure",
                Date = new DateTime(2021, 1, 1),
                Editor = new Editor() { Name = "Ubisoft" },
                Reviews = new List<Review>()
                {
                    new Review() { Id = 1, Text = "It's cool" },
                    new Review() { Id = 2, Text = "It's very cool" }
                }
            };

            _uncachedParse = new JsonRuleEngine() { UseCompiledExpressionCache = false };
            _cached = new JsonRuleEngine();
            _uncached = new JsonRuleEngine() { UseCompiledExpressionCache = false };
        }

        // ---- Expression building ----

        [Benchmark]
        public object Parse_Complex() => _uncachedParse.ParseExpression<Game>(_complexRule);

        [Benchmark]
        public object Parse_Simple() => _uncachedParse.ParseExpression<Game>(_simpleRule);

        // ---- Hot evaluation : without and with the compiled predicate cache ----

        [Benchmark]
        public bool Evaluate_Simple_NoCache() => _uncached.Evaluate(_game, _simpleRule);

        [Benchmark]
        public bool Evaluate_Simple_Cached() => _cached.Evaluate(_game, _simpleRule);

        [Benchmark]
        public bool Evaluate_Complex_NoCache() => _uncached.Evaluate(_game, _complexRule);

        [Benchmark]
        public bool Evaluate_Complex_Cached() => _cached.Evaluate(_game, _complexRule);

        [Benchmark]
        public bool Evaluate_ComplexJson_Cached() => _cached.Evaluate(_game, _complexJson);
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<EngineBenchmarks>();
        }
    }
}
