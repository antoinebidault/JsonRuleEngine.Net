using JsonRuleEngine.Net.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// Compiled predicate cache.
    ///
    /// Compiling a LINQ expression to a delegate (Expression.Compile) costs milliseconds :
    /// it was by far the dominant cost of every Evaluate call. The cache keys compiled
    /// delegates by (input type, rule content, compiler), so evaluating the same rules
    /// repeatedly compiles only once.
    ///
    /// The key is derived from the CONTENT of the rules (their json), never from the
    /// object identity : mutating a rule and evaluating again gives a different key,
    /// so the change is always honored.
    ///
    /// The cache is only used when the expression depends on nothing but the rules :
    /// evaluations using EvaluateOptions, CustomPropertyAccessor or
    /// CustomConditionRuleSetAccessor always compile, because the produced expression
    /// depends on user code the cache key cannot capture.
    /// </summary>
    public partial class JsonRuleEngine
    {
        /// <summary>
        /// Set to false to disable the compiled predicate cache for this engine instance.
        /// The cache itself is shared by all instances (it is keyed by rule content)
        /// </summary>
        public bool UseCompiledExpressionCache { get; set; } = true;

        /// <summary>
        /// When the cache reaches this size it is cleared before inserting,
        /// which bounds the memory used by pathological dynamic rule streams
        /// </summary>
        private const int CompiledCacheMaxSize = 512;

        private static readonly ConcurrentDictionary<CompiledCacheKey, Delegate> CompiledCache
            = new ConcurrentDictionary<CompiledCacheKey, Delegate>();

        private struct CompiledCacheKey : IEquatable<CompiledCacheKey>
        {
            public Type Type;
            public string Rules;

            public bool Equals(CompiledCacheKey other)
                => Type == other.Type && Rules == other.Rules;

            public override bool Equals(object obj) => obj is CompiledCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (Rules != null ? Rules.GetHashCode() : 0);
                }
            }
        }

        private bool CanUseCompiledCache<T>(EvaluateOptions<T> evaluateOptions)
        {
            return UseCompiledExpressionCache
                && evaluateOptions == null
                && CustomPropertyAccessor == null
                && CustomConditionRuleSetAccessor == null;
        }

        /// <summary>
        /// Get the compiled predicate for a rule object, from the cache when possible
        /// </summary>
        private Func<T, bool> GetPredicate<T>(ConditionRuleSet rules, EvaluateOptions<T> evaluateOptions)
        {
            if (!CanUseCompiledCache(evaluateOptions))
            {
                return ParseExpression<T>(rules, evaluateOptions).Compile();
            }

            string json;
            try
            {
                json = JsonConvert.SerializeObject(rules);
            }
            catch
            {
                // Unserializable rule content : compile without caching
                return ParseExpression<T>(rules, null).Compile();
            }

            return (Func<T, bool>)GetOrCompile(typeof(T), json, () => ParseExpression<T>(rules, null).Compile());
        }

        /// <summary>
        /// Get the compiled predicate for a json rule string, from the cache when possible.
        /// On a cache hit, the json is not even parsed
        /// </summary>
        private Func<T, bool> GetPredicate<T>(string jsonRules, EvaluateOptions<T> evaluateOptions)
        {
            if (!CanUseCompiledCache(evaluateOptions))
            {
                return ParseExpression<T>(jsonRules, evaluateOptions).Compile();
            }

            return (Func<T, bool>)GetOrCompile(typeof(T), jsonRules, () => ParseExpression<T>(jsonRules, null).Compile());
        }

        private Delegate GetOrCompile(Type type, string rulesKey, Func<Delegate> compile)
        {
            var key = new CompiledCacheKey()
            {
                Type = type,
                Rules = rulesKey
            };

            if (CompiledCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var compiled = compile();

            if (CompiledCache.Count >= CompiledCacheMaxSize)
            {
                CompiledCache.Clear();
            }

            CompiledCache[key] = compiled;
            return compiled;
        }
    }
}
