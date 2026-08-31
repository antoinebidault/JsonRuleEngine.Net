using JsonRuleEngine.Net.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// The expression compiler :
    /// - single pass : the rule tree is compiled during one descent,
    /// - the <see cref="CustomConditionRuleSetAccessor"/> is probed exactly once per leaf,
    ///   before any field validation, so claimed fields never need to exist on T,
    /// - groups are bound with short circuiting AndAlso / OrElse expressions,
    /// - the reflection walk that detects collections in a field path is cached.
    /// </summary>
    public partial class JsonRuleEngine
    {
        /// <summary>
        /// Cache of the reflection walk detecting the first collection segment of a field path.
        /// Static : the result only depends on (type, field)
        /// </summary>
        private static readonly ConcurrentDictionary<CollectionPathKey, ValueTuple<string, Type>> CollectionPathCache
            = new ConcurrentDictionary<CollectionPathKey, ValueTuple<string, Type>>();

        private struct CollectionPathKey : IEquatable<CollectionPathKey>
        {
            public Type Type;
            public string Field;

            public bool Equals(CollectionPathKey other) => Type == other.Type && Field == other.Field;
            public override bool Equals(object obj) => obj is CollectionPathKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (Field != null ? Field.GetHashCode() : 0);
                }
            }
        }

        /// <summary>
        /// Cached version of <see cref="GetCollectionType"/>
        /// (transformers and claimed fields are peeled off before this is called)
        /// </summary>
        private static ValueTuple<string, Type> GetCollectionPath(Type type, string field)
        {
            return CollectionPathCache.GetOrAdd(
                new CollectionPathKey() { Type = type, Field = field },
                key => GetCollectionType(key.Type, key.Field));
        }

        /// <summary>
        /// Rules of one group that target the same collection : they are compiled
        /// into a single Any / All call (same element semantics under And)
        /// </summary>
        private sealed class CollectionBucket
        {
            public string Prefix;
            public Type ElementType;
            public bool FromRootLeaf;
            public readonly List<ConditionRuleSet> Rules = new List<ConditionRuleSet>();
        }

        private static ConditionRuleSet CloneLeaf(ConditionRuleSet rule)
        {
            return new ConditionRuleSet()
            {
                Field = rule.Field,
                Operator = rule.Operator,
                Value = rule.Value,
                Separator = rule.Separator
            };
        }

        /// <summary>
        /// Tree compilation : one descent, no pre pass
        /// </summary>
        private Expression ParseTree<T>(ConditionRuleSet condition, ParameterExpression parm, EvaluateOptions<T> evaluateOptions)
        {
            bool isOr = condition.Separator == ConditionRuleSeparator.Or;
            Expression Bind(Expression left, Expression right)
                => left == null ? right
                 : right == null ? left
                 : isOr ? Expression.OrElse(left, right) : (Expression)Expression.AndAlso(left, right);

            bool isRootLeaf = condition.Rules == null || !condition.Rules.Any();
            var children = isRootLeaf ? new[] { condition } : (IEnumerable<ConditionRuleSet>)condition.Rules;

            Expression result = null;
            List<CollectionBucket> buckets = null;

            foreach (var child in children)
            {
                // Nested group
                if (child.Separator.HasValue && child.Rules != null && child.Rules.Any())
                {
                    result = Bind(result, ParseTree<T>(child, parm, evaluateOptions));
                    continue;
                }

                // 1. Whole rule accessor : probed exactly once per leaf, before any field validation,
                //    so the claimed field does not need to exist on T
                var custom = InvokeConditionRuleSetAccessor(child, parm, false, false);
                if (custom != null)
                {
                    result = Bind(result, custom);
                    continue;
                }

                // A leaf without field contributes nothing
                if (string.IsNullOrEmpty(child.Field))
                {
                    continue;
                }

                try
                {
                    // 2. EvaluateOptions transformer
                    if (evaluateOptions != null && evaluateOptions.HasTransformer(child.Field))
                    {
                        var transformer = evaluateOptions.GetTransformer<T>(child.Field, parm);
                        var visitor = new ParameterReplaceVisitor(parm);
                        Expression newBody = visitor.Visit(transformer);
                        result = Bind(result, CompileExpression(newBody, new List<string>() { child.Field }, false, parm, child.Operator, child.Value, true, CloneLeaf(child), false));
                        continue;
                    }

                    // 3. Collection detection (cached reflection walk).
                    //    Throws InvalidField for an unknown field
                    var (prefix, elementType) = GetCollectionPath(typeof(T), child.Field);

                    if (string.IsNullOrEmpty(prefix))
                    {
                        // Plain leaf : walk the dotted path with null guards, then apply the operator
                        var clone = CloneLeaf(child);
                        var fields = child.Field.Split('.').ToList();
                        bool isDict = IsDictionary(parm.Type);
                        Expression expression = null;
                        while (fields.Count > 0)
                        {
                            expression = CompileExpression(expression ?? parm, fields, isDict, parm, child.Operator, child.Value, false, clone, false);
                        }

                        result = Bind(result, expression);
                        continue;
                    }

                    // 4. Rule on a collection : buffered, sibling rules on the same collection
                    //    are compiled into a single Any / All (same element semantics under And)
                    buckets = buckets ?? new List<CollectionBucket>();
                    CollectionBucket bucket = null;
                    foreach (var b in buckets)
                    {
                        if (b.Prefix == prefix)
                        {
                            bucket = b;
                            break;
                        }
                    }

                    if (bucket == null)
                    {
                        bucket = new CollectionBucket() { Prefix = prefix, ElementType = elementType, FromRootLeaf = isRootLeaf };
                        buckets.Add(bucket);
                    }

                    bucket.Rules.Add(child);
                }
                catch (Exception e) when (!(e is JsonRuleEngineException))
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {child.Field} : {e.Message} ");
                }
            }

            if (buckets != null)
            {
                foreach (var bucket in buckets)
                {
                    result = Bind(result, CompileCollectionBucket(bucket, parm, condition.Separator));
                }
            }

            return result;
        }

        /// <summary>
        /// Compile the rules of one collection into a single Any / All expression :
        /// the merged rule is handed to the collection machinery (HandleTableRule)
        /// </summary>
        private Expression CompileCollectionBucket(CollectionBucket bucket, ParameterExpression parm, ConditionRuleSeparator? separator)
        {
            try
            {
                var collectionRules = new List<ConditionRuleSet>();
                var merged = new ConditionRuleSet()
                {
                    Field = bucket.Prefix,
                    Separator = separator,
                    CollectionRules = collectionRules
                };

                foreach (var rule in bucket.Rules)
                {
                    var sub = new ConditionRuleSet()
                    {
                        Field = rule.Field.Length > bucket.Prefix.Length + 1
                            ? rule.Field.Substring(bucket.Prefix.Length + 1)
                            : "",
                        Operator = rule.Operator,
                        Value = rule.Value
                    };

                    if (bucket.ElementType != null)
                    {
                        if (!IsClass(bucket.ElementType))
                        {
                            // Collection of primitives : the operator applies to the element itself
                            sub.Field = "";
                        }
                        else
                        {
                            // The sub field may cross a nested collection
                            sub = RegroupFieldsByCollection(bucket.ElementType, sub);
                        }
                    }

                    sub.Separator = separator;
                    collectionRules.Add(sub);
                }

                if (bucket.FromRootLeaf)
                {
                    // Single root rule : the original operator drives the null guards
                    // of the prefix walk (isNull propagates through null parents)
                    merged.Operator = bucket.Rules[0].Operator;
                }

                var fields = bucket.Prefix.Split('.').ToList();
                Expression expression = null;
                while (fields.Count > 0)
                {
                    expression = CompileExpression(expression ?? parm, fields, false, parm, merged.Operator, merged.Value, false, merged, false);
                }

                return expression;
            }
            catch (Exception e) when (!(e is JsonRuleEngineException))
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {bucket.Prefix} : {e.Message} ");
            }
        }
    }
}
