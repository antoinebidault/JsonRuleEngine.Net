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

            // If multiple rules inner
            if (condition.Rules != null && condition.Rules.Any())
            {
                foreach (var rule in condition.Rules)
                {
                    left = bind(left, CreateRuleExpression<T>(rule, parm));
                }
            }
            else
            {
                left = bind(left, CreateRuleExpression<T>(condition, parm));
            }

            return left;
        }

        private static Expression CreateRuleExpression<T>(ConditionRuleSet rule, ParameterExpression parm)
        {
            Expression right = null;
            if (rule.Separator.HasValue && rule.Rules != null && rule.Rules.Any())
            {
                right = ParseTree<T>(rule, parm);
                return right;
            }

            // If the field is empty, then return true
            if (string.IsNullOrEmpty(rule.Field))
            {
                return right;
            }

            MemberExpression property = null;

            try
            {
                string field = rule.Field;
                var fields = field.Split('.');
                int i = 0;
                foreach (var member in fields)
                {
                    i = i + member.Length + 1;
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
                        string subField = "";
                        try
                        {
                            subField = field.Substring(i, field.Length - i);
                            if (string.IsNullOrEmpty(subField))
                            {
                                throw new Exception("");
                            }
                        }
                        catch
                        {
                            throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The array {field} does not have a subfield. e.g. {field}.Id");
                        }
                        right = HandleTableRule(rule, subField, rule.Value, property);
                        return right;
                    }
                }


            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {rule.Field} : {e.Message} ");
            }

            return CreateOperationExpression(property, rule.Operator, rule.Value);
        }


        /// <summary>
        /// Apply the con
        /// </summary>
        /// <returns></returns>
        private static Expression CreateOperationExpression(MemberExpression property, ConditionRuleOperator op, object value)
        {
            Expression expression = null;

            // Contains methods
            // Need a conversion to an array of string
            if (op == ConditionRuleOperator.@in || op == ConditionRuleOperator.notIn)
            {

                // Parsing the array
                try
                {
                    var listType = typeof(IEnumerable<>).MakeGenericType(property.Type);
                    var array = ((JArray)value).ToObject(listType);
                    MethodInfo method = null;
                    method = MethodContains.MakeGenericMethod(property.Type);


                    // var executed = Expression.Call(property, "ToString", Type.EmptyTypes);
                    expression = Expression.Call(
                        method,
                        Expression.Constant(array),
                         property);

                    if (op == ConditionRuleOperator.notIn)
                    {
                        expression = Expression.Not(expression);
                    }

                    return expression;
                }
                catch (Exception e)
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, $"The provided value is not an array {value.ToString()} : {e.Message} ");
                }

            }

            // It's a bit tricky behaviour
            // If it's a nullable prop, scope to the .Value of the prop just if not a isNull operator
            if (property.Type.IsNullable() &&
                (op != ConditionRuleOperator.isNotNull &&
                op != ConditionRuleOperator.isNull))
            {
                property = Expression.Property(property, "Value");
            }

            value = property.Type.GetValue(value);
            var toCompare = Expression.Constant(value);

            if (op == ConditionRuleOperator.isNull)
            {
                expression = Expression.Equal(property, Expression.Default(property.Type));
            }
            else if (op == ConditionRuleOperator.isNotNull)
            {
                expression = Expression.NotEqual(property, Expression.Default(property.Type));
            }
            else if (op == ConditionRuleOperator.equal)
            {
                expression = Expression.Equal(property, toCompare);
            }
            else if (op == ConditionRuleOperator.notEqual)
            {
                expression = Expression.NotEqual(property, toCompare);
            }
            else if (op == ConditionRuleOperator.greaterThan)
            {
                expression = Expression.GreaterThan(property, toCompare);
            }
            else if (op == ConditionRuleOperator.greaterThanInclusive)
            {
                expression = Expression.GreaterThanOrEqual(property, toCompare);
            }
            else if (op == ConditionRuleOperator.lessThan)
            {
                expression = Expression.LessThan(property, toCompare);
            }
            else if (op == ConditionRuleOperator.lessThanInclusive)
            {
                expression = Expression.LessThanOrEqual(property, toCompare);
            }
            else if (op == ConditionRuleOperator.contains)
            {
                MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                expression = Expression.Call(property, method, toCompare);
            }
            else if (op == ConditionRuleOperator.doesNotContains)
            {
                MethodInfo method = typeof(string).GetMethod("Except", new[] { typeof(string) });
                expression = Expression.Call(property, method, toCompare);
            }
            return expression;
        }

        private static MethodCallExpression HandleTableRule(ConditionRuleSet rule, string field, object value, MemberExpression property)
        {
            // Get the type of T in the IEnumerable<T> or ICollection<T> list
            var childType = property.Type.GetGenericArguments().First();

            // Set it as the param of the any expression
            var param = Expression.Parameter(childType);
            var anyExpression = Expression.Lambda(GetExpression(rule, param, field, value), param);


            var anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
            anyMethod = anyMethod.MakeGenericMethod(childType);

            var predicate = Expression.Call(anyMethod, property, anyExpression);

            return predicate;
        }


        private static Expression GetExpression(ConditionRuleSet rule, ParameterExpression param, string field, object value)
        {
            MemberExpression property = null;

            foreach (var member in field.Split('.'))
            {
                if (property == null)
                {
                    property = Expression.Property(param, member);
                }
                else
                {
                    property = Expression.Property(property, member);
                }
            }

            return CreateOperationExpression(property, rule.Operator, value);
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