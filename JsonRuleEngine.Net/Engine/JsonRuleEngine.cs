using JsonRuleEngine.Net.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// The JsonRuleEngine class that contains
    /// </summary>
    public class JsonRuleEngine
    {
        /// <summary>
        /// Validate expression against a list of white listed field
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonRules"></param>
        /// <param name="fieldWhiteList"></param>
        /// <returns></returns>
        public ValidateExpressionResult ValidateExpressionFields(string jsonRules, IEnumerable<string> fieldWhiteList)
        {
            var data = Parse(jsonRules);
            if (data == null)
            {
                return ValidateExpressionResult.Valid;
            }

            return ValidateExpressionRecursive(data, fieldWhiteList);
        }

        private ValidateExpressionResult ValidateExpressionRecursive(ConditionRuleSet rule, IEnumerable<string> fieldWhiteList)
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
        /// <param name="evaluateOptions"></param>
        /// <returns>Expression function</returns>
        public Expression<Func<T, bool>> ParseExpression<T>(string jsonRules, EvaluateOptions<T> evaluateOptions = null)
        {
            return ParseExpression<T>(Parse(jsonRules), evaluateOptions);
        }

        /// <summary>
        /// Transform the ConditionRuleSet object to an expression function 
        /// that can be evaluated in LinqToSql queries or whatever
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rules"></param>
        /// <param name="evaluateOptions"></param>
        /// <returns></returns>
        public Expression<Func<T, bool>> ParseExpression<T>(ConditionRuleSet rules, EvaluateOptions<T> evaluateOptions = null)
        {
            var itemExpression = Expression.Parameter(typeof(T));
            var conditions = ParseTree<T>(rules, itemExpression, evaluateOptions);

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
        /// <param name="evaluateOptions"></param>
        /// <returns>True if the conditions are matched</returns>
        public bool Evaluate<T>(T obj, string jsonRules, EvaluateOptions<T> evaluateOptions = null)
        {
            var query = ParseExpression<T>(jsonRules, evaluateOptions);
            return query.Compile().Invoke(obj);
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="jsonRules">The json string conditionRuleSet object</param>
        /// <param name="evaluateOptions"></param>
        /// <returns>True if the conditions are matched</returns>
        public TOut Evaluate<T, TOut>(T obj, string jsonRules, EvaluateOptions<T> evaluateOptions = null)
        {
            var rules = Parse<TOut>(jsonRules);
            return Evaluate<T, TOut>(obj, rules, evaluateOptions);
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
        public bool TryEvaluate<T, TOut>(T obj, string jsonRules, out TOut returnValue)
        {
            var rules = Parse<TOut>(jsonRules);
            return TryEvaluate<T, TOut>(obj, rules, out returnValue);
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="rules">The conditionRuleSet object</param>
        /// <param name="evaluateOptions"></param>
        /// <returns>True if the conditions are matched</returns>
        public TOut Evaluate<T, TOut>(T obj, ConditionRuleSet<TOut> rules, EvaluateOptions<T> evaluateOptions)
        {
            var query = ParseExpression<T>(rules, evaluateOptions);
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
        public bool TryEvaluate<T, TOut>(T obj, ConditionRuleSet<TOut> rules, out TOut returnValue)
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
        public bool Evaluate(object obj, ConditionRuleSet rules)
        {
            MethodInfo method = typeof(JsonRuleEngine).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(m => m.Name == nameof(JsonRuleEngine.Evaluate) &&
                        m.GetParameters() != null &&
                        m.ContainsGenericParameters &&
                      m.GetParameters().Length == 3 &&
                     m.GetParameters().Select(c => c.ParameterType).Contains(typeof(ConditionRuleSet)));

            MethodInfo generic = method.MakeGenericMethod(obj.GetType());
            return (bool)generic.Invoke(this, parameters: new[] { obj, rules, null });
        }

        /// <summary>
        /// Test the conditions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The object to test</param>
        /// <param name="rules">The conditionRuleSet object</param>
        /// <param name="evaluateOptions"></param>
        /// <returns>True if the conditions are matched</returns>
        public bool Evaluate<T>(T obj, ConditionRuleSet rules, EvaluateOptions<T> evaluateOptions = null)
        {
            var query = ParseExpression<T>(rules, evaluateOptions);
            return query.Compile().Invoke(obj);
        }

        private readonly MethodInfo MethodContains = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Contains)
                            && m.GetParameters().Length == 2);

        private readonly MethodInfo MethodAny = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Any)
                            && m.GetParameters().Length == 1);

        private readonly MethodInfo MethodNotContains = typeof(Enumerable).GetMethods(
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
        /// <param name="evaluateOptions"></param>
        /// <returns></returns>
        private Expression ParseTree<T>(
        ConditionRuleSet condition,
        ParameterExpression parm
        , EvaluateOptions<T> evaluateOptions)
        {
            condition = RegroupFieldsByCollection(typeof(T), condition, evaluateOptions?.GetAllTransformers());

            Binder binder = condition.Separator == ConditionRuleSeparator.Or ? (Binder)Expression.Or : Expression.And;

            Expression bind(Expression lef, Expression right) => lef == null ? right : binder(lef, right);

            Expression left = null;

            // If multiple rules inner
            if (condition.Rules != null && condition.Rules.Any())
            {
                foreach (var rule in condition.Rules)
                {
                    left = bind(left, CreateRuleExpression<T>(rule, parm, evaluateOptions));
                }
            }
            else
            {
                left = bind(left, CreateRuleExpression<T>(condition, parm, evaluateOptions));
            }

            return left;
        }



        /// <summary>
        /// Take each condition and regroup them by filters on collection
        /// { field: "Reviews.Id"}, {field: "Reviews.Type"} => { field: "Reviews", collectionRules: [ { field: "Id"}, {field: "Type"}] }
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="type"></param>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        private static ConditionRuleSet RegroupFieldsByCollection(Type type, ConditionRuleSet condition, IDictionary<string, Expression> dictionary = null)
        {

            var conditionRuleSet = new ConditionRuleSet()
            {
                Field = condition.Field,
                Operator = condition.Operator,
                CollectionRules = condition.CollectionRules,
                Value = condition.Value,
                Rules = condition.Rules,
                Separator = condition.Separator
            };

            if (!string.IsNullOrEmpty(conditionRuleSet.Field))
            {
                var (field, currentType) = GetCollectionType(type, conditionRuleSet.Field, dictionary);
                if (!string.IsNullOrEmpty(field))
                {

                    string subField = "";

                    if (conditionRuleSet.Field.Length > field.Length + 1)
                    {
                        subField = condition.Field.Substring(field.Length + 1, condition.Field.Length - field.Length - 1);
                    }
                    conditionRuleSet.Field = field;
                    conditionRuleSet.Value = null;
                    conditionRuleSet.Separator = condition.Separator;
                    conditionRuleSet.Rules = null;
                    conditionRuleSet.CollectionRules = new List<ConditionRuleSet>() {
                      RegroupFieldsByCollection(currentType,  new ConditionRuleSet()
                        {
                            Field = subField,
                            Operator = condition.Operator,
                            Value = condition.Value
                        })
                     };

                    type = currentType;
                }
            }

            if (conditionRuleSet.Rules == null && conditionRuleSet.CollectionRules == null)
            {
                return conditionRuleSet;
            }

            var rules = conditionRuleSet.Rules ?? conditionRuleSet.CollectionRules;
            var groups = rules.GroupBy(m => GetCollectionType(type, m.Field, dictionary));

            foreach (var group in groups)
            {
  
                if (group.Key.Item1 == string.Empty)
                {
                    for (var i = 0; i < group.Count(); i++)
                    {
                        var rule = group.ElementAt(i);
                        rules = rules.Append(rule);
                    }
                }
                else
                {
                    conditionRuleSet.Field = group.Key.Item1;

                    for (var i = 0; i < group.Count(); i++)
                    {
                        var rule = group.ElementAt(i);
                        if (rule.Field.Length > group.Key.Item1.Length + 1)
                        {
                            rule.Field = rule.Field.Substring(group.Key.Item1.Length + 1, rule.Field.Length - group.Key.Item1.Length - 1);
                        }
                        else
                        {
                            rule.Field = "";
                        }
                        var arrType = group.Key.Item2;
                        if (arrType != null)
                        {
                            var regroupField = RegroupFieldsByCollection(arrType, new ConditionRuleSet()
                            {
                                Field = rule.Field,
                                Operator = rule.Operator,
                                Value = rule.Value
                            });
                            rule.CollectionRules = regroupField.CollectionRules;
                            rule.Field = regroupField.Field;
                            rule.Operator = regroupField.Operator;
                            rule.Value = regroupField.Value;
                        }
                        rule.Separator = condition.Separator;
                    }
                    conditionRuleSet.Rules = null;
                    conditionRuleSet.CollectionRules = group.ToList();
                }
            }

            return conditionRuleSet;
        }

        private static (string, Type) GetCollectionType(Type type, string field, IDictionary<string, Expression> dictionary)
        {
            var output = "";
            var currentType = type;
            var oldFields = new List<string>();

            if (field == null)
            {
                return (output, currentType);
            }

            if (IsDictionary(type))
            {
                return (output, currentType);
            }


            if (dictionary != null && dictionary.ContainsKey(field))
            {
                return (output, currentType);
            }

            var remainingFields = field.Split('.').ToList();

            while (remainingFields.Count > 0)
            {

                var prop = currentType.GetProperty(remainingFields[0]);

                oldFields.Add(remainingFields[0]);
                remainingFields.Remove(remainingFields[0]);

                if (prop != null)
                {
                    currentType = prop.PropertyType;
                }
                else if (currentType  == null || !IsDictionary(currentType))
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, field);
                }

                if (currentType.IsArray())
                {
                    output = string.Join(".", oldFields);
                    remainingFields.Clear();
                }
            }

            return (output, currentType.GetGenericArguments().FirstOrDefault());
        }

        /// <summary>
        /// Do not delete !
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static object GetValueOrDefault(IDictionary dictionary, string key)
        {
            if (dictionary.Contains(key))
            {
                return dictionary[key];
            }
            return null;
        }

        public static T GetValueOrDefaultObject<T>(Dictionary<string, object> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
            {
                return (T)dictionary[key];
            }
            return default(T);
        }

        private Expression CreateRuleExpression<T>(ConditionRuleSet rule, ParameterExpression parm, EvaluateOptions<T> evaluateOptions)
        {
            Expression right = null;
            if (rule.Separator.HasValue && rule.Rules != null && rule.Rules.Any())
            {
                right = ParseTree<T>(rule, parm, evaluateOptions);
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

                if (evaluateOptions != null && evaluateOptions.HasTransformer(field))
                {
                    var transformer = evaluateOptions.GetTransformer<T>(field, parm);

                    var visitor = new ParameterReplaceVisitor(parm);
                    Expression newBody = visitor.Visit(transformer);

                    expression = CompileExpression(newBody, new List<string> { field }, false, parm, rule.Operator, rule.Value, true, rule);
                }
                else
                {
                    var fields = field.Split('.').ToList();
                    bool isDict = IsDictionary(parm.Type);

                    while (fields.Count > 0)
                    {
                        expression = CompileExpression(expression ?? parm, fields, isDict, parm, rule.Operator, rule.Value, false, rule);
                    }
                }

                return expression;
            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {rule.Field} : {e.Message} ");
            }
        }

        private static bool IsDictionary(Type type)
        {
            return typeof(IDictionary).IsAssignableFrom(type);
        }

        /// <summary>
        /// You can setup here a custom property accessor
        /// For example, for using library like https://github.com/iozcelik/EntityFrameworkCore.SqlServer.JsonExtention
        /// (string fieldName, inputPara) => {  EF.Functions.JsonValue(w.ExtraInformation, "InternetTLD") }
        /// </summary>
        /// <return>Expresssion</return>
        public Func<PropertyAccessorContext, Expression> CustomPropertyAccessor { get; set; }

        private Expression CompileExpression(Expression expression, List<string> remainingFields, bool isDict, Expression inputParam, ConditionRuleOperator op, object value, bool isOverride, ConditionRuleSet rule)
        {
            string memberName = remainingFields.First();

            // If a custom accessor is set
            if (this.CustomPropertyAccessor != null)
            {
                var tmpExpression = this.CustomPropertyAccessor.Invoke(new PropertyAccessorContext()
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
                var type = GetDictionaryType(value, op);
                var methodGetValue = (this.GetType()).GetMethod(nameof(GetValueOrDefaultObject)).MakeGenericMethod(type);
                expression = Expression.Call(methodGetValue, expression, key);
            }
            else if (isDict)
            {
                Expression key = Expression.Constant(memberName);
                var methodGetValue = (this.GetType()).GetMethod("GetValueOrDefault");


                expression = Expression.Call(methodGetValue, inputParam, key);
            }
            else if (expression == null)
            {
                expression = Expression.Property(inputParam, memberName);
            }
            else if (!isOverride)
            {
                expression = Expression.Property(expression, memberName);
            }

            if (expression.Type.IsArray())
            {
                if (isDict)
                    rule.CollectionRules = new List<ConditionRuleSet>()
                    {
                        new ConditionRuleSet()
                        {
                            Operator = op,
                            Value = value,
                             Field = memberName
                        }
                    };

                if (rule.CollectionRules.Count() > 0)
                {
                    return HandleTableRule(expression, inputParam, op, value, remainingFields, isOverride, rule, isDict);
                }
            }

            remainingFields.Remove(memberName);
            if (remainingFields.Count == 0)
            {
                return CreateOperationExpression(expression, op, value);
            }
            else
            {
                if (op == ConditionRuleOperator.isNull)
                {
                    return Expression.OrElse(Expression.Equal(expression, Expression.Constant(null)), CompileExpression(expression, remainingFields, isDict, inputParam, op, value, isOverride, rule));
                }
                return Expression.AndAlso(Expression.NotEqual(expression, Expression.Constant(null)), CompileExpression(expression, remainingFields, isDict, inputParam, op, value, isOverride, rule));
            }
        }

        private Type GetDictionaryType(object value, ConditionRuleOperator op)
        {
            if (value == null)
            {
                return typeof(string);
            }

            // Specific case
            // Try to parse the number if greater than or less than is used
            if (op == ConditionRuleOperator.greaterThan ||
                op == ConditionRuleOperator.lessThan ||
                op == ConditionRuleOperator.lessThanInclusive ||
                op == ConditionRuleOperator.greaterThanInclusive)
            {
                if (value.IsStringInt())
                {
                    return typeof(int);
                }
                if (value.IsStringDouble())
                {
                    return typeof(double);
                }
            }

            if (value is JArray)
                return typeof(IEnumerable<>).MakeGenericType(GetJArrayType((JArray)value));

            return value.GetType();
        }

        private Type GetJArrayType(JArray array)
        {
            var firstValue = array.FirstOrDefault();
            if (firstValue != null)
            {
                switch (firstValue.Type)
                {
                    case JTokenType.String:
                        return typeof(string);
                    case JTokenType.Float:
                        return typeof(double);
                    case JTokenType.Object:
                        return typeof(object);
                    case JTokenType.Date:
                        return typeof(DateTime);
                    case JTokenType.Boolean:
                        return typeof(Boolean);
                    case JTokenType.Integer:
                        return typeof(int);
                    case JTokenType.Array:
                        return typeof(JArray);
                }
            }
            return null;
        }

        /// <summary>
        /// Handle table rule
        /// </summary>
        /// <param name="array"></param>
        /// <param name="param"></param>
        /// <param name="op"></param>
        /// <param name="isOverride"></param>
        /// <param name="value"></param>
        /// <param name="remainingFields"></param>
        /// <returns></returns>
        /// <exception cref="JsonRuleEngineException"></exception>
        private Expression HandleTableRule(Expression array, Expression param, ConditionRuleOperator op, object value, List<string> remainingFields, bool isOverride, ConditionRuleSet rule, bool isDict)
        {
            var currentField = remainingFields.First();
            var childType = GetChildType(array, value);

            // Set it as the param of the any expression
            var childParam = Expression.Parameter(childType);

            var expressions = new List<Expression>();
            var expressionsEmpty = new List<Expression>();

            Expression exp = null;
            MethodInfo anyMethod = null;
            foreach (var collectionRule in rule.CollectionRules.OrderBy(m => IsEmptyOperator(m)))
            {
                // Contains methods
                // Need a conversion to an array of string
                if (IsEmptyOperator(collectionRule))
                {

                    // Parsing the array
                    try
                    {
                        remainingFields.Clear();
                        var MethodAny = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 1);
                        var method = MethodAny.MakeGenericMethod(childType);

                        var expression = Expression.Call(method, array);


                        // All other expressions are cancelled in that case
                        if (collectionRule.Operator == ConditionRuleOperator.isEmpty)
                        {
                            return Expression.OrElse(Expression.Equal(array, Expression.Constant(null)), Expression.Not(expression));
                        }

                        if (rule.CollectionRules.Count() == 1)
                        {
                            return Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), expression);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, $"The provided value is invalid : {e.Message} ");
                    }
                }

                // True if it is a class
                if (IsClass(childType))
                {
                    var fields = collectionRule.Field.Split('.').ToList();
                    while (fields.Count > 0)
                    {
                        exp = CompileExpression(childParam, fields, isDict, param, collectionRule.Operator, collectionRule.Value, false, collectionRule);
                    }
                }
                else
                {
                    exp = CreateOperationExpression(childParam, collectionRule.Operator, collectionRule.Value);
                }


                // In case it's a different of notEqual operator, we would like to apply the .All
                if (collectionRule.Operator == ConditionRuleOperator.notIn || collectionRule.Operator == ConditionRuleOperator.notEqual)
                {
                    anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "All" && m.GetParameters().Length == 2);
                    anyMethod = anyMethod.MakeGenericMethod(childType);
                    exp = Expression.Call(anyMethod, array, Expression.Lambda(exp, childParam));
                }

                expressions.Add(exp);
            }

            exp = expressions.First();
            foreach (var expression in expressions.Skip(1))
            {
                if (rule.Separator == ConditionRuleSeparator.And)
                {
                    exp = Expression.AndAlso(exp, expression);
                }
                else
                {
                    exp = Expression.OrElse(exp, expression);
                }
            }

            var anyExpression = Expression.Lambda(exp, childParam);

            remainingFields.Remove(currentField);

            anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
            anyMethod = anyMethod.MakeGenericMethod(childType);

            return Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), Expression.Call(anyMethod, array, anyExpression));
        }

        private static bool IsEmptyOperator(ConditionRuleSet collectionRule)
        {
            return collectionRule.Operator == ConditionRuleOperator.isNotEmpty ||
                                collectionRule.Operator == ConditionRuleOperator.isEmpty;
        }

        private Type GetChildType(Expression array, object value)
        {
            return array.Type == typeof(JArray) ? GetJArrayType((JArray)value) : array.Type.GetGenericArguments().First();
        }

        private Expression CreateTableCondition(Expression array, Expression param, object value, ConditionRuleOperator op, string currentField, List<string> remainingFields, ConditionRuleSet condition)
        {
            var childType = array.Type == typeof(JArray) ? GetJArrayType((JArray)value) : array.Type.GetGenericArguments().First();


            // Set it as the param of the any expression
            var childParam = Expression.Parameter(childType);
            Expression exp = null;
            MethodInfo anyMethod = null;
            Expression anyExpression = null;
            remainingFields.Remove(currentField);

            // True if it is a class
            if (IsClass(childType))
            {
                while (remainingFields.Count > 0)
                {
                    exp = CompileExpression(exp ?? childParam, remainingFields, false, param, op, value, false, condition);
                }
                anyExpression = Expression.Lambda(exp, childParam);
            }
            else
            {
                exp = CreateOperationExpression(exp ?? childParam, op, value);
                anyExpression = Expression.Lambda(exp, childParam);
            }

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

        private bool IsClass(Type type)
        {
            return type.IsClass && !type.IsArray && !type.IsAbstract && !type.IsEnum && type != typeof(string);
        }

        /// <summary>
        /// Create an operation expression from the input prop to value
        /// </summary>
        /// <param name="inputProperty"></param>
        /// <param name="op"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="JsonRuleEngineException"></exception>
        private Expression CreateOperationExpression(Expression inputProperty, ConditionRuleOperator op, object value)
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
                    object array;
                    if (value is JArray)
                    {
                        var listType = typeof(IEnumerable<>).MakeGenericType(inputProperty.Type);
                        array = ((JArray)value).ToObject(listType);
                    }
                    else
                    {
                        array = value;
                    }

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
                if (property.Type == typeof(DateTime) || property.Type == typeof(DateTime?))
                {
                    var dateStr = value.ToString();

                    bool isDatePeriod = false;

                    // It's a date
                    if (valueType == typeof(string) && dateStr.StartsWith("\""))
                    {
                        value = ParseTimeSpan(dateStr);
                        if (dateStr.EndsWith(".00:00:00\""))
                        {
                            isDatePeriod = true;
                            value = ((DateTime)value).Date;
                        }
                    }

                    // Date format "yyyy-mm-dd"
                    if (isDatePeriod || (dateStr.Length == 10 && dateStr.IndexOf("-") == 4))
                    {
                        if (property.Type.IsNullable())
                        {
                            property = Expression.Property(Expression.Property(property, "Value"), "Date");
                        }
                        else
                        {
                            property = Expression.Property(property, "Date");
                        }
                    }
                }
            }

            bool isNullable = property.Type.IsNullable();
            if (value != null && isNullable)
            {
                value = Nullable.GetUnderlyingType(property.Type).GetValue(value);
            }
            else
            {
                value = property.Type.GetValue(value);
            }

            Expression toCompare = Expression.Constant(value);
            if (toCompare.Type != property.Type)
            {
                toCompare = Expression.Convert(Expression.Constant(value), property.Type);
            }

            if (op == ConditionRuleOperator.isNull)
            {
                if (!isMethodCall && isNullable)
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
                if (!isMethodCall && isNullable)
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
                if (property.Type == typeof(string))
                {
                    var notExp = Expression.NotEqual(property, Expression.Default(property.Type));
                    expression = Expression.AndAlso(notExp, expression);
                }
            }
            else if (op == ConditionRuleOperator.doesNotContains)
            {
                MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                expression = Expression.Not(Expression.Call(property, method, toCompare));
                if (property.Type == typeof(string))
                {
                    var notExp = Expression.NotEqual(property, Expression.Default(property.Type));
                    expression = Expression.AndAlso(notExp, expression);
                }
            }

            return expression;
        }

        private DateTime ParseTimeSpan(string str)
        {
            TimeSpan ts = JsonConvert.DeserializeObject<TimeSpan>(str);
            return DateTime.UtcNow.Add(ts);
        }

        private string[] ToArray(object obj)
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
        private ConditionRuleSet Parse(string jsonRules)
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

        private ConditionRuleSet<TOut> Parse<TOut>(string jsonRules)
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
