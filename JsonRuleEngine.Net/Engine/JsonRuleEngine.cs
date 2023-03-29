using Newtonsoft.Json;
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

            // Breaking change
            // If no conditions parsed
            // Let's return a default predicate
            if (conditions == null)
            {
                return (m) => default;
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
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
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
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="jsonRules">The json string conditionRuleSet object</param>
        /// <returns>True if the conditions are matched</returns>
        public static TOut Evaluate<T, TOut>(T obj, string jsonRules)
        {
            var rules = Parse<TOut>(jsonRules);
            return Evaluate<T, TOut>(obj, rules);
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="jsonRules">The json string conditionRuleSet object</param>
        /// <param name="returnValue">The json string conditionRuleSet object</param>
        /// <returns>True if the conditions are matched</returns>
        public static bool TryEvaluate<T, TOut>(T obj, string jsonRules, out TOut returnValue)
        {
            var rules = Parse<TOut>(jsonRules);
            return JsonRuleEngine.TryEvaluate<T, TOut>(obj, rules, out returnValue);
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="rules">The conditionRuleSet object</param>
        /// <returns>True if the conditions are matched</returns>
        public static TOut Evaluate<T, TOut>(T obj, ConditionRuleSet<TOut> rules)
        {
            var query = ParseExpression<T>(rules);
            var result = query.Compile().Invoke(obj);
            if (result)
            {
                var returnValue = (TOut)Convert.ChangeType(rules.ReturnValue.Value, rules.ReturnValue.Type);
                return returnValue;
            }
            else
            {
                return default(TOut);
            }
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="rules">The conditionRuleSet object</param>
        /// <returns>True if the conditions are matched</returns>
        public static bool TryEvaluate<T, TOut>(T obj, ConditionRuleSet<TOut> rules, out TOut returnValue)
        {
            returnValue = default;
            var query = ParseExpression<T>(rules);
            var success = query.Compile().Invoke(obj);

            if (!success)
                return success;

            returnValue = (TOut)Convert.ChangeType(rules.ReturnValue.Value, rules.ReturnValue.Type);

            return success;
        }

        /// <summary>
        /// Evaluate a simple object (it will uses the inferred type)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="rules"></param>
        /// <returns></returns>
        public static bool Evaluate(object obj, ConditionRuleSet rules)
        {
            MethodInfo method = typeof(JsonRuleEngine).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(m => m.Name == nameof(JsonRuleEngine.Evaluate) &&
                        m.GetParameters() != null &&
                        m.ContainsGenericParameters &&
                      m.GetParameters().Length == 2 &&
                     m.GetParameters().Select(c => c.ParameterType).Contains(typeof(ConditionRuleSet)));

            MethodInfo generic = method.MakeGenericMethod(obj.GetType());
            return (bool)generic.Invoke(null, parameters: new[] { obj, rules });
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

        private static readonly MethodInfo MethodAny = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Any)
                            && m.GetParameters().Length == 1);

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

        /// <summary>
        /// Do not delete !
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetValueOrDefault(IDictionary dictionary, string key)
        {
            if (dictionary.Contains(key))
            {
                return dictionary[key]?.ToString();
            }
            return null;
        }

        public static T GetValueOrDefaultObject<T>(Dictionary<string,object> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
            {
                return (T)dictionary[key];
            }
            return default(T);
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

            Expression expression = null;

            try
            {
                string field = rule.Field;
                var fields = field.Split('.').ToList();
                bool isDict = typeof(IDictionary).IsAssignableFrom(parm.Type);

                while (fields.Count > 0)
                {
                    expression = CompileExpression(expression ?? parm, fields, isDict, parm, rule.Operator, rule.Value);
                }

                return expression;
            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {rule.Field} : {e.Message} ");
            }
        }

        /// <summary>
        /// You can setup here a custom property accessor
        /// For example, for using library like https://github.com/iozcelik/EntityFrameworkCore.SqlServer.JsonExtention
        /// (string fieldName, inputPara) => {  EF.Functions.JsonValue(w.ExtraInformation, "InternetTLD") }
        /// </summary>
        /// <return>Expresssion</return>
        public static Func<PropertyAccessorContext, Expression> CustomPropertyAccessor { get; set; }

        private static Expression CompileExpression(Expression expression, List<string> remainingFields, bool isDict, Expression inputParam, ConditionRuleOperator op, object value)
        {
            string memberName = remainingFields.First();

            // If a custom accessor is set
            if (JsonRuleEngine.CustomPropertyAccessor != null)
            {
                var tmpExpression = JsonRuleEngine.CustomPropertyAccessor.Invoke(new PropertyAccessorContext()
                {
                    ValueCompared = value,
                    MemberName = memberName,
                    Expression = expression,
                    InputParam = inputParam
                });
                if (tmpExpression != null)
                {
                    remainingFields.Remove(memberName);
                    return CreateOperationExpression(tmpExpression, op, value);
                }
            }

            if (expression != null && typeof(Dictionary<string, object>).IsAssignableFrom(expression.Type))
            {
                Expression key = Expression.Constant(memberName);
                var methodGetValue = (typeof(JsonRuleEngine)).GetMethod("GetValueOrDefaultObject").MakeGenericMethod(value?.GetType() ?? typeof(string));
                expression = Expression.Call(methodGetValue, expression, key);
            }
            else if (isDict)
            {
                Expression key = Expression.Constant(memberName);
                var methodGetValue = (typeof(JsonRuleEngine)).GetMethod("GetValueOrDefault");

                expression = Expression.Call(methodGetValue, inputParam, key);
            }
            else if (expression == null)
            {
                expression = Expression.Property(inputParam, memberName);
            }
            else
            {
                expression = Expression.Property(expression, memberName);
            }

    

            if (expression.Type.IsArray())
            {
                return HandleTableRule(expression, inputParam, op, value, remainingFields);
            }

            remainingFields.Remove(memberName);
            if (remainingFields.Count == 0)
            {
                return CreateOperationExpression(expression, op, value);
            }
            else
            {
                return expression;
            }
        }


        /// <summary>
        /// Handle table rule
        /// </summary>
        /// <param name="array"></param>
        /// <param name="param"></param>
        /// <param name="op"></param>
        /// <param name="value"></param>
        /// <param name="remainingFields"></param>
        /// <returns></returns>
        /// <exception cref="JsonRuleEngineException"></exception>
        private static Expression HandleTableRule(Expression array, Expression param, ConditionRuleOperator op, object value, List<string> remainingFields)
        {

            var currentField = remainingFields.First();
            var childType = array.Type.GetGenericArguments().First();

            // Contains methods
            // Need a conversion to an array of string
            if (op == ConditionRuleOperator.isNotEmpty ||
                op == ConditionRuleOperator.isEmpty)
            {

                // Parsing the array
                try
                {
                    remainingFields.Clear();

                    var MethodAny = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 1);
                    var method = MethodAny.MakeGenericMethod(childType);

                    var expression = Expression.Call(method, array);

                    if (op == ConditionRuleOperator.isEmpty)
                    {
                        return Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), Expression.Not(expression));
                    }

                    return Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), expression);
                }
                catch (Exception e)
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, $"The provided value is invalid : {e.Message} ");
                }
            }

            // Set it as the param of the any expression
            var childParam = Expression.Parameter(childType);
            remainingFields.Remove(currentField);
            Expression exp = null;
            while (remainingFields.Count > 0)
            {
                exp = CompileExpression(exp ?? childParam, remainingFields, false, param, op, value);
            }
            var anyExpression = Expression.Lambda(exp, childParam);
            MethodInfo anyMethod = null;

            // In case it's a different of notEqual operator, we would like to apply the .All
            if (op == ConditionRuleOperator.notIn || op == ConditionRuleOperator.notEqual)
            {
                anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "All" && m.GetParameters().Length == 2);
                anyMethod = anyMethod.MakeGenericMethod(childType);
                return Expression.Call(anyMethod, array, anyExpression);
            }

            anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
            anyMethod = anyMethod.MakeGenericMethod(childType);

            return Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), Expression.Call(anyMethod, array, anyExpression));
        }


        /// <summary>
        /// Create an operation expression from the input prop to value
        /// </summary>
        /// <param name="inputProperty"></param>
        /// <param name="op"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="JsonRuleEngineException"></exception>
        private static Expression CreateOperationExpression(Expression inputProperty, ConditionRuleOperator op, object value)
        {
            Expression expression = null;

            // Contains methods
            // Need a conversion to an array of string
            if (op == ConditionRuleOperator.@in ||
                op == ConditionRuleOperator.notIn)
            {

                // Parsing the array
                try
                {
                    var listType = typeof(IEnumerable<>).MakeGenericType(inputProperty.Type);
                    var array = ((JArray)value).ToObject(listType);
                    MethodInfo method = null;
                    method = MethodContains.MakeGenericMethod(inputProperty.Type);

                    expression = Expression.Call(
                        method,
                        Expression.Constant(array),
                         inputProperty);

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


            var property = inputProperty;
            bool isMethodCall = property is MethodCallExpression;



            // Specific case of TimeSpan Stored as "\00:22:00"\""
            if (value != null)
            {
                var valueType = value.GetType();
                if (((property.Type == typeof(DateTime) || (property.Type == typeof(DateTime?))) && valueType == typeof(string) && value.ToString().StartsWith("\"")))
                {
                    value = ParseDate(value.ToString());
                }
            }


            if (value != null && inputProperty.Type.IsNullable())
            {
                value = Nullable.GetUnderlyingType(inputProperty.Type).GetValue(value);
            }
            else
            {
                value = property.Type.GetValue(value);
            }

            Expression toCompare = Expression.Constant(value);
            if (toCompare.Type != inputProperty.Type)
            {
                toCompare = Expression.Convert(Expression.Constant(value), inputProperty.Type);
            }

            if (op == ConditionRuleOperator.isNull)
            {
                if (!isMethodCall && inputProperty.Type.IsNullable())
                {
                    expression = Expression.Not(Expression.Property(property, "HasValue"));
                }
                else
                {
                    expression = Expression.Equal(property, Expression.Default(property.Type));
                }
            }
            else if (op == ConditionRuleOperator.isNotNull)
            {
                if (!isMethodCall && inputProperty.Type.IsNullable())
                {
                    expression = Expression.Property(property, "HasValue");
                }
                else
                {
                    expression = Expression.NotEqual(property, Expression.Default(property.Type));
                }
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
                MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                expression = Expression.Not(Expression.Call(property, method, toCompare));
            }

            return expression;
        }


        private static DateTime ParseDate(string str)
        {
            TimeSpan ts = JsonConvert.DeserializeObject<TimeSpan>(str);
            return DateTime.UtcNow.Add(ts);
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

        private static ConditionRuleSet<TOut> Parse<TOut>(string jsonRules)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<ConditionRuleSet<TOut>>(jsonRules);
            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidJsonRules, $"Invalid json provided : {e.Message}");
            }
        }
    }
}