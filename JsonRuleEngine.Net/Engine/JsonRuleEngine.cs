using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace JsonRuleEngine.Net
{
    public static class JsonRuleEngine
    {
        private static readonly MethodInfo MethodContains = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Contains)
                            && m.GetParameters().Length == 2);

        private static readonly MethodInfo MethodNotContains = typeof(Enumerable).GetMethods(
                    BindingFlags.Static | BindingFlags.Public)
                    .Single(m => m.Name == nameof(Enumerable.Except)
                        && m.GetParameters().Length == 2);

        private delegate Expression Binder(Expression left, Expression right);


        /// <summary>
        /// Parse the expression tree
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="condition"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expression ParseTree<T>(
        ConditionRuleSet condition,
        ParameterExpression parm)
        {
            IEnumerable<ConditionRuleSet> rules = condition.Rules;

            Binder binder = condition.Separator == ConditionRuleSeparator.And ? (Binder)Expression.And : Expression.Or;

            Expression bind(Expression lef, Expression right) => lef == null ? right : binder(lef, right);

            Expression left = null;
            foreach (var rule in rules)
            {
                if (rule.Separator.HasValue)
                {
                    var right = ParseTree<T>(rule, parm);
                    left = bind(left, right);
                    continue;
                }

                string field = rule.Field;
                object value = rule.Value;

                MemberExpression property = null;
                try
                {
                    property = Expression.Property(parm, field);
                }
                catch (Exception e)
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {field} : {e.Message} ");
                }

                // Contains methods
                // Need a conversion to an array of string
                if (rule.Operator == ConditionRuleOperator.@in || rule.Operator == ConditionRuleOperator.notIn)
                {
                    IEnumerable<string> array = null;

                    // Parsing the array
                    try
                    {
                        array = ((JToken)rule.Value).Values<string>();
                    }
                    catch (Exception e)
                    {
                        throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, $"The provided value is not an array {value.ToString()} : {e.Message} ");
                    }

                    MethodInfo method = null;
                    if (rule.Operator == ConditionRuleOperator.@in)
                    {
                        method = MethodContains.MakeGenericMethod(typeof(string));
                    }
                    else
                    {
                        method = MethodNotContains.MakeGenericMethod(typeof(string));
                    }

                    var right = Expression.Call(
                        method,
                        Expression.Constant(array),
                        property);
                    left = bind(left, right);
                }
                else
                {
                    object val = value is bool || value is string ?
                        (object)value.ToString() : double.Parse(value.ToString());
                    var toCompare = Expression.Constant(val);
                    Expression right = null;
                    if (rule.Operator == ConditionRuleOperator.equal)
                    {
                        right = Expression.Equal(property, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.notEqual)
                    {
                        right = Expression.NotEqual(property, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.greaterThan)
                    {
                        right = Expression.GreaterThan(property, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.greaterThanInclusive)
                    {
                        right = Expression.GreaterThanOrEqual(property, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.lessThan)
                    {
                        right = Expression.LessThan(property, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.lessThanInclusive)
                    {
                        right = Expression.LessThanOrEqual(property, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.contains)
                    {
                        MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                        right = Expression.Call(property, method, toCompare);
                    }
                    else if (rule.Operator == ConditionRuleOperator.doesNotContains)
                    {
                        MethodInfo method = typeof(string).GetMethod("Except", new[] { typeof(string) });
                        right = Expression.Call(property, method, toCompare);
                    }
                    left = bind(left, right);
                }
            }

            return left;
        }

        private static string[] ToArray(object obj)
        {
            return ((IEnumerable)obj).Cast<object>()
                              .Select(x => x.ToString())
                              .ToArray();
        }

        public static Expression<Func<T, bool>> ParseExpression<T>(string json)
        {
            return ParseExpression<T>(Parse(json));
        }

        public static Expression<Func<T, bool>> ParseExpression<T>(ConditionRuleSet doc)
        {
            var itemExpression = Expression.Parameter(typeof(T));
            var conditions = ParseTree<T>(doc, itemExpression);
            if (conditions.CanReduce)
            {
                conditions = conditions.ReduceAndCheck();
            }

            Console.WriteLine(conditions.ToString());

            var query = Expression.Lambda<Func<T, bool>>(conditions, itemExpression);
            return query;
        }

        /// <summary>
        /// Transform to predicate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static Func<T, bool> ParsePredicate<T>(string json)
        {
            var query = ParseExpression<T>(json);
            return query.Compile();
        }

        public static Func<T, bool> ParsePredicate<T>(ConditionRuleSet json)
        {
            var query = ParseExpression<T>(json);
            return query.Compile();
        }


        public static bool Evaluate<T>(T obj, string json)
        {
            var query = ParseExpression<T>(json);
            return query.Compile().Invoke(obj);
        }

        public static bool Evaluate<T>(T obj, ConditionRuleSet json)
        {
            var query = ParseExpression<T>(json);
            return query.Compile().Invoke(obj);
        }
        private static ConditionRuleSet Parse(string json)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<ConditionRuleSet>(json);
            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidJsonRules, "Invalid json provided");
            }
        }

    }
}