using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace JsonRuleEngine.Net
{

    /// <summary>
    /// The JsonRuleEngine class that contains
    /// </summary>
    public static class JsonRuleEngine
    {
        /// <summary>
        /// Validate expression against a list of white listed field
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonRules"></param>
        /// <param name="fieldWhiteList"></param>
        /// <returns></returns>
        public static ValidateExpressionResult ValidateExpressionFields(string jsonRules, IEnumerable<string> fieldWhiteList)
        {
            var data = Parse(jsonRules);
            if (data == null)
            {
                return ValidateExpressionResult.Valid;
            }

            return ValidateExpressionRecursive(data, fieldWhiteList);
        }

        private static ValidateExpressionResult ValidateExpressionRecursive(ConditionRuleSet rule, IEnumerable<string> fieldWhiteList)
        {
            if (!string.IsNullOrEmpty(rule.Field) && !fieldWhiteList.Contains(rule.Field))
            {
                return new ValidateExpressionResult()
                {
                    InvalidField = rule.Field
                };
            }

            if (rule.Rules != null)
            {
                foreach (var subRule in rule.Rules)
                {
                    var evaluate = ValidateExpressionRecursive(subRule, fieldWhiteList);
                    if (!evaluate.Success)
                    {
                        return evaluate;
                    }
                }
            }

            return ValidateExpressionResult.Valid;
        }


        /// <summary>
        /// Transform the ConditionRuleSet object to an expression function 
        /// that can be evaluated in LinqToSql queries or whatever
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonRules"></param>
        /// <returns>Expression function</returns>
        public static Expression<Func<T, bool>> ParseExpression<T>(string jsonRules)
        {
            return ParseExpression<T>(Parse(jsonRules));
        }

        /// <summary>
        /// Transform the ConditionRuleSet object to an expression function 
        /// that can be evaluated in LinqToSql queries or whatever
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rules"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> ParseExpression<T>(ConditionRuleSet rules)
        {
            var itemExpression = Expression.Parameter(typeof(T));
            var conditions = ParseTree<T>(rules, itemExpression);

            // If no conditions parsed
            // Let's return a true predicate
            if (conditions == null)
            {
                return (m) => true;
            }

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
        /// <returns>A predicate that return the true if the conditions are matched</returns>
        public static Func<T, bool> ParsePredicate<T>(string jsonRules)
        {
            var query = ParseExpression<T>(jsonRules);
            return query.Compile();
        }

        /// <summary>
        /// Transform a ConditionRuleSet object to predicate
        /// </summary>
        /// <returns>A predicate that return the true if the conditions are matched</returns>
        public static Func<T, bool> ParsePredicate<T>(ConditionRuleSet rules)
        {
            var query = ParseExpression<T>(rules);
            return query.Compile();
        }


        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="jsonRules">The json string conditionRuleSet object</param>
        /// <returns>True if the conditions are matched</returns>
        public static bool Evaluate<T>(T obj, string jsonRules)
        {
            var query = ParseExpression<T>(jsonRules);
            return query.Compile().Invoke(obj);
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="rules">The conditionRuleSet object</param>
        /// <returns>True if the conditions are matched</returns>
        public static bool Evaluate<T>(T obj, ConditionRuleSet rules)
        {
            var query = ParseExpression<T>(rules);
            return query.Compile().Invoke(obj);
        }


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

            Binder binder = condition.Separator == ConditionRuleSeparator.Or ? (Binder)Expression.Or : Expression.And;

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
                bool moveNext = false;

                try
                {
                    foreach (var member in field.Split('.'))
                    {

                        if (property == null)
                        {
                            property = Expression.Property(parm, member);
                        }
                        else
                        {
                            property = Expression.Property(property, member);
                        }

                        if (property.Type.IsArray())
                        {
                            MethodCallExpression predicate = HandleTableRule(rule, field, value, property);
                            left = bind(left, predicate);
                            moveNext = true;
                            break;
                        }
                    }


                }
                catch (Exception e)
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {field} : {e.Message} ");
                }

                if (moveNext)
                {
                    continue;
                }

                // Contains methods
                // Need a conversion to an array of string
                if (rule.Operator == ConditionRuleOperator.@in || rule.Operator == ConditionRuleOperator.notIn)
                {

                    // Parsing the array
                    try
                    {
                        var listType = typeof(IEnumerable<>).MakeGenericType(property.Type);
                        var array = ((JArray)rule.Value).ToObject(listType);
                        MethodInfo method = null;
                        method = MethodContains.MakeGenericMethod(property.Type);


                        // var executed = Expression.Call(property, "ToString", Type.EmptyTypes);
                        Expression right = Expression.Call(
                            method,
                            Expression.Constant(array),
                             property);

                        if (rule.Operator == ConditionRuleOperator.notIn)
                        {
                            right = Expression.Not(right);
                        }

                        left = bind(left, right);
                    }
                    catch (Exception e)
                    {
                        throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, $"The provided value is not an array {value.ToString()} : {e.Message} ");
                    }

                }
                else
                {

                    // It's a bit tricky behaviour
                    // If it's a nullable prop, scope to the .Value of the prop just if not a isNull operator
                    if (property.Type.IsNullable() &&
                        (rule.Operator != ConditionRuleOperator.isNotNull &&
                        rule.Operator != ConditionRuleOperator.isNull))
                    {
                        property = Expression.Property(property, "Value");
                    }

                    value = GetValue(property.Type, value);
                    var toCompare = Expression.Constant(value);

                    Expression right = null;
                    if (rule.Operator == ConditionRuleOperator.isNull)
                    {
                        right = Expression.Equal(property, Expression.Default(property.Type));
                    }
                    else if (rule.Operator == ConditionRuleOperator.isNotNull)
                    {
                        right = Expression.NotEqual(property, Expression.Default(property.Type));
                    }
                    else if (rule.Operator == ConditionRuleOperator.equal)
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

        private static MethodCallExpression HandleTableRule(ConditionRuleSet rule, string field, object value, MemberExpression property)
        {
            var childType = property.Type.GetGenericArguments().First();
            var param = Expression.Parameter(childType);

            // Get the prop to filter
            var idProp = Expression.PropertyOrField(param, field.Split('.').Last());
            var newValue = GetValue(idProp.Type, value);
            Expression anyExp = null;
            var toCompare = Expression.Constant(newValue);
            if (rule.Operator == ConditionRuleOperator.contains || rule.Operator == ConditionRuleOperator.doesNotContains)
            {
                if (rule.Operator == ConditionRuleOperator.contains)
                {
                    anyExp = Expression.Equal(idProp, toCompare);
                }
                else
                {
                    anyExp = Expression.NotEqual(idProp, toCompare);
                }
            }
            else
            {
                var listType = typeof(IEnumerable<>).MakeGenericType(idProp.Type);
                var array = ((JArray)rule.Value).ToObject(listType);
                MethodInfo method = null;
                method = MethodContains.MakeGenericMethod(idProp.Type);
                
                anyExp = Expression.Call(
                    method,
                    Expression.Constant(array),
                     idProp);

                if (rule.Operator == ConditionRuleOperator.notIn)
                {
                    anyExp = Expression.Not(anyExp);
                }
            }



            var anyExpression = Expression.Lambda(anyExp, param);
            var anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
            anyMethod = anyMethod.MakeGenericMethod(childType);
            var predicate = Expression.Call(anyMethod, property, anyExpression);
            return predicate;
        }

        private static bool IsArray(this Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static object GetValue(Type type, object value)
        {
            try
            {
                if (type == typeof(DateTime?))
                {
                    DateTime? output = null;
                    if (value != null || value.ToString() != "")
                    {
                        output = DateTime.Parse(value.ToString());
                    }

                    return output;
                }

                if (type == typeof(DateTime))
                {
                    return DateTime.Parse(value.ToString());
                }


                if (type == typeof(Guid) || type == typeof(Guid?))
                {
                    return Guid.Parse(value.ToString());
                }

                return Convert.ChangeType(value, type);
            }
            catch
            {
                if (type.IsValueType)
                {
                    return Activator.CreateInstance(type);
                }
                return null;
            }
        }




        private static bool IsNullable(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        private static string[] ToArray(object obj)
        {
            return ((IEnumerable)obj).Cast<object>()
                              .Select(x => x.ToString())
                              .ToArray();
        }

        /// <summary>
        /// Json parsing
        /// </summary>
        /// <param name="jsonRules"></param>
        /// <returns></returns>
        private static ConditionRuleSet Parse(string jsonRules)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<ConditionRuleSet>(jsonRules);
            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidJsonRules, $"Invalid json provided : {e.Message}");
            }
        }

    }
}