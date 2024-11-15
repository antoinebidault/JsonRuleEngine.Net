using JsonRuleEngine.Net.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
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
        /// <param name="returnValue">The conditionRuleSet object</param>
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
                    return conditionRuleSet;
                }
            }

            if (conditionRuleSet.Rules == null && conditionRuleSet.CollectionRules == null)
            {
                return conditionRuleSet;
            }

            var rules = conditionRuleSet.Rules ?? conditionRuleSet.CollectionRules;
            var groups = rules.GroupBy(m => GetCollectionType(type, m.Field, dictionary));

            var newRuleSet = new List<ConditionRuleSet>();
            foreach (var group in groups)
            {

                if (group.Key.Item1 == string.Empty)
                {
                    for (var i = 0; i < group.Count(); i++)
                    {
                        var rule = group.ElementAt(i);
                        newRuleSet.Add(rule);
                    }
                }
                else
                {
                    var newConditionRuleSet = new ConditionRuleSet();

                    newConditionRuleSet.Field = group.Key.Item1;
                    newConditionRuleSet.CollectionRules = new List<ConditionRuleSet>();

                    for (var i = 0; i < group.Count(); i++)
                    {
                        var sourceRule = group.ElementAt(i);
                        var rule = new ConditionRuleSet()
                        {
                            CollectionRules = sourceRule.CollectionRules,
                            Field = sourceRule.Field,
                            Operator = sourceRule.Operator,
                            Rules = sourceRule.Rules,
                            Separator = sourceRule.Separator,
                            Value = sourceRule.Value
                        };

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
                            if (!IsClass(group.Key.Item2))
                            {
                                rule.Field = "";
                                rule.CollectionRules = null;
                            }
                            else
                            {
                                var regroupField = RegroupFieldsByCollection(arrType, new ConditionRuleSet()
                                {
                                    Field = rule.Field,
                                    Operator = rule.Operator,
                                    Value = rule.Value
                                });
                                rule.Rules = null;
                                rule.CollectionRules = regroupField.CollectionRules;
                                rule.Field = regroupField.Field;
                                rule.Operator = regroupField.Operator;
                                rule.Value = regroupField.Value;
                            }

                        }
                        rule.Separator = condition.Separator;
                        newConditionRuleSet.Separator = condition.Separator;
                        newConditionRuleSet.CollectionRules = newConditionRuleSet.CollectionRules.Append(rule);
                    }
                    newConditionRuleSet.Rules = null;
                    newRuleSet.Add(newConditionRuleSet);
                }
            }
            conditionRuleSet.Rules = newRuleSet;
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
                else if (currentType == null || !IsDictionary(currentType))
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
                object value = dictionary[key];
                if (value is T)
                {
                    return (T)value;
                }


                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (InvalidCastException)
                {
                    throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue,
                        $"The type provided in dictionary key {key} ({value.GetType().Name}) " +
                        $"is not the same as the value provided in rule ({typeof(T).Name})");
                }

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

#if !DEBUG
            try
            {
#endif
            string field = rule.Field;

            if (evaluateOptions != null && evaluateOptions.HasTransformer(field))
            {
                var transformer = evaluateOptions.GetTransformer<T>(field, parm);

                var visitor = new ParameterReplaceVisitor(parm);
                Expression newBody = visitor.Visit(transformer);

                expression = CompileExpression(newBody, new List<string> { field }, false, parm, rule.Operator, rule.Value, true, rule, false);
            }
            else
            {
                var fields = field.Split('.').ToList();
                bool isDict = IsDictionary(parm.Type);

                while (fields.Count > 0)
                {
                    expression = CompileExpression(expression ?? parm, fields, isDict, parm, rule.Operator, rule.Value, false, rule, false);
                }
            }

            return expression;
#if !DEBUG
            }
            catch (Exception e)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidField, $"The provided field is invalid {rule.Field} : {e.Message} ");
            }
#endif
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

        private Expression CompileExpression(Expression expression, List<string> remainingFields, bool isDict, Expression inputParam, ConditionRuleOperator op, object value, bool isOverride, ConditionRuleSet rule, bool isNavigation)
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
                    if (isNavigation)
                    {
                        return tmpExpression;
                    }
                    else
                    {
                        return CreateOperationExpression(tmpExpression, op, value);
                    }
                }
            }


            if (expression != null && typeof(Dictionary<string, object>).IsAssignableFrom(expression.Type))
            {

                remainingFields.Remove(memberName);
                return GetDictionaryOperation(expression, memberName, op, value);
                /*
                Expression key = Expression.Constant(memberName);
                var type = GetDictionaryType(value, op);
                var methodGetValue = (this.GetType()).GetMethod(nameof(GetValueOrDefaultObject)).MakeGenericMethod(type);
                expression = Expression.Call(methodGetValue, expression, key);
                */
                //isDict = true;
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

                if (rule.CollectionRules != null && rule.CollectionRules.Count() > 0)
                {
                    return HandleTableRule(expression, inputParam, op, value, remainingFields, isOverride, rule, isDict);
                }
            }

            remainingFields.Remove(memberName);

            if (remainingFields.Count == 0)
            {
                if (isNavigation)
                {
                    return expression;
                }
                else
                {

                    return CreateOperationExpression(expression, op, value);
                }
            }
            else
            {
                if (op == ConditionRuleOperator.isNull)
                {
                    return Expression.OrElse(Expression.Equal(expression, Expression.Constant(null)), CompileExpression(expression, remainingFields, isDict, inputParam, op, value, isOverride, rule, isNavigation));
                }
                return Expression.AndAlso(Expression.NotEqual(expression, Expression.Constant(null)), CompileExpression(expression, remainingFields, isDict, inputParam, op, value, isOverride, rule, isNavigation));
            }
        }


        /// <summary>
        /// For handling dictionary specific case,
        /// the operation handling needs to be a bit different
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="memberName"></param>
        /// <param name="op"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        private Expression GetDictionaryOperation(Expression expression, string memberName, ConditionRuleOperator op, object value)
        {    // Access the ContainsKey method of the dictionary
            var containsKeyMethod = typeof(IDictionary<string, object>).GetMethod("ContainsKey");

            // Create a call to the ContainsKey method
            var keyExpression = Expression.Constant(memberName);
            var containsKeyExpression = Expression.Call(expression, containsKeyMethod, keyExpression);

            // Get the IDictionary item by key (if it exists)
            var dictionaryAccess = Expression.MakeIndex(
                expression,
                typeof(IDictionary<string, object>).GetProperty("Item"),
                new[] { keyExpression });


            // Add special case for the isNull and isNotNull operators
            var comparison = GetDictionaryComparisonExpression(dictionaryAccess, value, op);


            var valueIsNotACollection = Expression.Not(Expression.TypeIs(dictionaryAccess, typeof(object[])));


            IEnumerable<object> valueCollection = new List<object>();

            // Specific cas of collection
            if (value != null)
            {
                if (!value.GetType().IsArray())
                {
                    valueCollection = valueCollection.Append(value);
                }
                else
                {

                    valueCollection = value is JArray ? ((JArray)value).ToObject<IEnumerable<object>>() : (IEnumerable<object>)value;
                }


                // Check if value is a collection and contains "cocoonut"
                var containsMethod = typeof(Enumerable).GetMethods()
                                                           .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                                                           .MakeGenericMethod(typeof(object));


                var list = new List<Expression>().Any(m => m.CanReduce);
                var valueAsCollection = Expression.Convert(dictionaryAccess, typeof(IEnumerable<object>));

                Expression checkCollection = null;
                foreach (var itemValue in valueCollection)
                {
                    Expression call = Expression.Call(null, containsMethod, valueAsCollection, Expression.Convert(Expression.Constant(itemValue), typeof(object)));

                    if (op == ConditionRuleOperator.notIn || op == ConditionRuleOperator.excludeAll)
                    {
                        call = Expression.Not(call);
                    }

                    if (checkCollection == null)
                    {
                        checkCollection = call;
                    }
                    else
                    {
                        if (op == ConditionRuleOperator.@in || op == ConditionRuleOperator.includeAll)
                        {
                            checkCollection = Expression.Or(checkCollection, call);
                        }
                        else if (op == ConditionRuleOperator.notIn || op == ConditionRuleOperator.excludeAll)
                        {
                            checkCollection = Expression.AndAlso(checkCollection, call);
                        }
                    }
                }



                if (op == ConditionRuleOperator.doesNotContains || op == ConditionRuleOperator.notIn || op == ConditionRuleOperator.notEqual)
                {
                    checkCollection = Expression.Not(checkCollection);
                }

                var valueIsArray = Expression.TypeIs(dictionaryAccess, typeof(object[]));
                var valueIsCollection = Expression.TypeIs(dictionaryAccess, typeof(IEnumerable<object>));
                Expression isArrayOrCollection = Expression.OrElse(valueIsArray, valueIsCollection);
                comparison = Expression.Condition(isArrayOrCollection, checkCollection, comparison);
            }

            // If the key is not present, return false or some default behavior
            Expression defaultExpression = Expression.Constant(false);

            // Combine the contains key check and the comparison
            return Expression.Condition(containsKeyExpression, comparison, defaultExpression);



            /*
            // Define the parameter for the lambda expression (IDictionary<string, object> dict)
            // var dictParam = Expression.Parameter(typeof(IDictionary<string, object>), "dict");

            // Define the key we're searching for ("test")
            var key = Expression.Constant(memberName, typeof(string));
            var inputType = GetDictionaryType(value, op);
            // Expression to retrieve the value from the dictionary: dict.TryGetValue("test", out object value)
            var tryGetValueMethod = typeof(IDictionary<string, object>).GetMethod("TryGetValue");
            var valueVar = Expression.Variable(typeof(object), "value");
            var castedValue = Expression.Convert(valueVar, inputType);
            var tryGetValueCall = Expression.Call(expression, tryGetValueMethod, key, valueVar);

            var valueIsNotACollection = Expression.Not(Expression.TypeIs(valueVar, typeof(object[])));

            Expression compareExpression = CreateOperationExpression(castedValue, op, value);


            //  var isNotNull = Expression.Not(Expression.Equal(valueVar, Expression.Constant(null)));

            // var valueCasted = GetValueCasted(valueVar);

            // Expression compareExpression = Expression.Equal(valueCasted, Expression.Constant(value));


            var checkString = Expression.AndAlso(valueIsNotACollection, compareExpression);


            checkString = Expression.AndAlso(Expression.Not(Expression.Equal(castedValue, Expression.Default(inputType))), checkString);
            
            var valueIsNotACollection = Expression.Not(Expression.TypeIs(valueVar, typeof(object[])));
            // Check if value is a collection and contains "cocoonut"
            var containsMethod = typeof(Enumerable).GetMethods()
                                                       .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                                                       .MakeGenericMethod(typeof(object));
            var list = new List<Expression>().Any(m => m.CanReduce);
            var valueAsCollection = Expression.Convert(valueVar, typeof(IEnumerable<object>));
            var checkCollection = Expression.Call(null, containsMethod, valueAsCollection, Expression.Convert(Expression.Constant(value), typeof(object)));

            var valueIsArray = Expression.TypeIs(valueVar, typeof(object[]));
            //var valueIsCollection = Expression.TypeIs(valueVar, typeof(IEnumerable<object>));
            //Expression isArrayOrCollection = Expression.OrElse(valueIsArray, valueIsCollection);

            var check = Expression.OrElse(checkString, Expression.AndAlso(valueIsArray, checkCollection));


            // Combine the TryGetValue and the value check: dict.TryGetValue && (check for string or collection)
            var ifThenElseExpression = Expression.Condition(tryGetValueCall, check, Expression.Constant(false));

            var body = Expression.AndAlso(tryGetValueCall, ifThenElseExpression);

            var block = Expression.Block(new[] { valueVar }, body);

            return block;*/
        }

        private Expression GetDictionaryComparisonExpression(Expression dictionaryAccess, object value, ConditionRuleOperator op)
        {
            Expression comparison;
            switch (op)
            {
                case ConditionRuleOperator.isNull:
                    // Check if the dictionary value is null
                    comparison = Expression.Equal(dictionaryAccess, Expression.Constant(null, typeof(object)));
                    break;

                case ConditionRuleOperator.isNotNull:
                    // Check if the dictionary value is NOT null
                    comparison = Expression.NotEqual(dictionaryAccess, Expression.Constant(null, typeof(object)));
                    break;
                case ConditionRuleOperator.doesNotContains:
                case ConditionRuleOperator.contains:
                    if (value == null || value.GetType() != typeof(string))
                    {
                        throw new ArgumentException("The 'contains' operator requires a non-null string value.");
                    }

                    // Ensure the value in the dictionary is a string and call String.Contains
                    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                    comparison = Expression.AndAlso(
                        Expression.TypeIs(dictionaryAccess, typeof(string)),
                        Expression.Call(
                            Expression.Convert(dictionaryAccess, typeof(string)),
                            containsMethod,
                            Expression.Constant((string)value)
                        )
                    );

                    if (op == ConditionRuleOperator.doesNotContains)
                    {
                        comparison = Expression.Not(comparison);
                    }

                    break;

                default:
                    // Handle other operators (Equal, NotEqual, GreaterThan, etc.) including nulls
                    if (value == null)
                    {
                        switch (op)
                        {
                            case ConditionRuleOperator.equal:
                            case ConditionRuleOperator.@in:
                            case ConditionRuleOperator.includeAll:
                                comparison = Expression.Equal(dictionaryAccess, Expression.Constant(null, typeof(object)));
                                break;
                            case ConditionRuleOperator.notEqual:
                            case ConditionRuleOperator.notIn:
                            case ConditionRuleOperator.excludeAll:
                                comparison = Expression.NotEqual(dictionaryAccess, Expression.Constant(null, typeof(object)));
                                break;
                            default:
                                throw new NotSupportedException($"Unsupported operator for null comparison: {op}");
                        }
                    }
                    else
                    {
                        var isNumeric = value.IsNumeric();
                        var valueType = value.GetType();

                        Expression convertedValue;
                        if (isNumeric)
                        {
                            convertedValue = Expression.Constant(Convert.ChangeType(value, typeof(double)), typeof(double));
                        }
                        else
                        {
                            convertedValue = Expression.Convert(Expression.Constant(value), valueType);
                        }

                        // It's a date
                        if (valueType == typeof(string))
                        {
                            bool isDatePeriod = false;
                            var dateStr = value.ToString();
                            if (valueType == typeof(string) && dateStr.StartsWith("\""))
                            {
                                value = ParseTimeSpan(dateStr);
                                if (dateStr.EndsWith(".00:00:00\""))
                                {
                                    isDatePeriod = true;
                                    value = ((DateTime)value).Date;
                                }
                                convertedValue = Expression.Constant(value, typeof(DateTime));
                            }

                            // Date format "yyyy-mm-dd"
                            if (isDatePeriod || (dateStr.Length == 10 && dateStr.IndexOf("-") == 4))
                            {
                                if (DateTime.TryParseExact(dateStr, "DD-MM-YYYY", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime valueCasted))
                                {
                                    value = valueCasted;
                                    convertedValue = Expression.Constant(valueCasted, typeof(DateTime));
                                }
                            }
                        }


                        switch (op)
                        {
                            case ConditionRuleOperator.@in:
                            case ConditionRuleOperator.equal:
                                comparison = Expression.Equal(GetDictionary(isNumeric, dictionaryAccess, value), convertedValue);
                                break;

                            case ConditionRuleOperator.notEqual:
                            case ConditionRuleOperator.notIn:
                                comparison = Expression.NotEqual(GetDictionary(isNumeric, dictionaryAccess, value), convertedValue);
                                break;

                            case ConditionRuleOperator.greaterThan:
                                comparison = Expression.GreaterThan(GetDictionary(isNumeric, dictionaryAccess, value), convertedValue);
                                break;

                            case ConditionRuleOperator.greaterThanInclusive:
                                comparison = Expression.GreaterThanOrEqual(GetDictionary(isNumeric, dictionaryAccess, value), convertedValue);
                                break;

                            case ConditionRuleOperator.lessThan:
                                comparison = Expression.LessThan(GetDictionary(isNumeric, dictionaryAccess, value), convertedValue);
                                break;

                            case ConditionRuleOperator.lessThanInclusive:
                                comparison = Expression.LessThanOrEqual(GetDictionary(isNumeric, dictionaryAccess, value), convertedValue);
                                break;

                            default:
                                throw new NotSupportedException($"Unsupported operator: {op}");
                        }
                    }
                    break;
            }

            return comparison;

            UnaryExpression GetDictionary(bool isNumeric, Expression dicAccess, object objValue)
            {

                if (isNumeric)
                {
                    // Get the method info for Convert.ChangeType
                    MethodInfo changeTypeMethod = typeof(Convert).GetMethod("ChangeType", new[] { typeof(object), typeof(Type) });

                    // Create a method call expression for Convert.ChangeType(param, targetType)
                    MethodCallExpression convertCall = Expression.Call(
                        changeTypeMethod,
                        dicAccess,
                        Expression.Constant(typeof(double))
                    );

                    // Cast the result of Convert.ChangeType to the actual target type (double)
                    return Expression.Convert(convertCall, typeof(double));

                }

                return Expression.Convert(dicAccess, objValue.GetType());
            }
        }

        private Expression GetValueCasted(ParameterExpression valueVar)
        {
            var types = new List<Type>()
            {
                typeof(string),
                typeof(DateTime),
                typeof(Guid),
                typeof(int?),
                typeof(long?),
                typeof(double?),
                typeof(int),
                typeof(long),
                typeof(double),
                typeof(bool)
            };

            Expression expression = Expression.Constant(null);
            foreach (var type in types)
            {
                expression = Expression.Condition(Expression.TypeIs(valueVar, type), Expression.Convert(valueVar, type), expression);
            }

            return expression;
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

            if (value is string && ((value.ToString().StartsWith("\"") && value.ToString().Contains(":")) || value.ToString().IndexOf("-") == 5))
            {
                return typeof(DateTime?);
            }

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
            ConditionRuleOperator? originalOp = null;

            // Set it as the param of the any expression
            var childParam = Expression.Parameter(childType);

            var expressions = new List<Expression>();
            var expressionsEmpty = new List<Expression>();
            var expressionsAll = new List<Tuple<ConditionRuleOperator, Expression>>();

            Expression exp = null;
            Expression tempExpression = null;
            MethodInfo anyMethod = null;
            bool isIncludeAllStatement = false;
            foreach (var collectionRule in rule.CollectionRules.OrderBy(m => IsEmptyOperator(m)))
            {
                isIncludeAllStatement = collectionRule.Operator == ConditionRuleOperator.includeAll || collectionRule.Operator == ConditionRuleOperator.excludeAll;
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
                if (isIncludeAllStatement)
                {
                    tempExpression = GetChildExpression(param, isDict, childParam, exp, collectionRule, true);
                    tempExpression = HandleIncludeAll(array, tempExpression, childParam, collectionRule.Value, collectionRule.Operator);
                    expressionsAll.Add(Tuple.Create(collectionRule.Operator, tempExpression));
                    remainingFields.Remove(currentField);
                }
                else if (IsClass(childType))
                {
                    tempExpression = GetChildExpression(param, isDict, childParam, exp, collectionRule, false);
                }
                else
                {

                    tempExpression = CreateOperationExpression(childParam, collectionRule.Operator, collectionRule.Value);
                }


                // In case it's a different of notEqual operator, we would like to apply the .All

                if (collectionRule.Operator == ConditionRuleOperator.notIn ||
                    collectionRule.Operator == ConditionRuleOperator.notEqual)
                {
                    anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "All" && m.GetParameters().Length == 2);
                    anyMethod = anyMethod.MakeGenericMethod(childType);
                    expressionsAll.Add(Tuple.Create(collectionRule.Operator, (Expression)Expression.Call(anyMethod, array, Expression.Lambda(tempExpression, childParam))));
                }
                else if (!isIncludeAllStatement)
                {
                    expressions.Add(tempExpression);
                }
            }

            if (expressions.Any())
            {
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


                anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
                anyMethod = anyMethod.MakeGenericMethod(childType);

                exp = Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), Expression.Call(anyMethod, array, anyExpression));
            }

            if (expressionsAll.Any())
            {
                foreach (var expression in expressionsAll)
                {
                    if (exp == null)
                    {
                        exp = expression.Item2;
                    }
                    else if (rule.Separator == ConditionRuleSeparator.And)
                    {
                        exp = Expression.AndAlso(exp, expression.Item2);
                    }
                    else
                    {
                        exp = Expression.OrElse(exp, expression.Item2);
                    }
                }
                if (expressionsAll.All(m => m.Item1 != ConditionRuleOperator.includeAll && m.Item1 != ConditionRuleOperator.excludeAll))
                    exp = Expression.OrElse(Expression.Equal(array, Expression.Constant(null)), exp);
                else
                    exp = Expression.AndAlso(Expression.NotEqual(array, Expression.Constant(null)), exp);
            }

            remainingFields.Remove(currentField);

            return exp;
        }

        private Expression GetChildExpression(Expression param, bool isDict, ParameterExpression childParam, Expression exp, ConditionRuleSet collectionRule, bool isNavigation = false)
        {
            var fields = collectionRule.Field.Split('.').ToList();
            while (fields.Count > 0)
            {
                exp = CompileExpression(childParam, fields, isDict, param, collectionRule.Operator, collectionRule.Value, false, collectionRule, isNavigation);
            }

            return exp;
        }
        private Expression GetChild(Expression exp, ConditionRuleSet collectionRule)
        {
            var fields = collectionRule.Field.Split('.').ToList();
            while (fields.Count > 0)
            {
                if (IsDictionary(exp.Type))
                {
                }
                else
                {
                    exp = Expression.Property(exp, fields[0]);
                }
                fields.RemoveAt(0);
            }

            return exp;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="visitedExpression">Example : Reviews.Id</param>
        /// <param name="childParam">Reviews</param>
        /// <param name="operator"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="JsonRuleEngineException"></exception>
        private Expression HandleIncludeAll(Expression arrayExpression, Expression visitedExpression, ParameterExpression childParam, object value, ConditionRuleOperator @operator)
        {
            if (value == null)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, "The provided value is not correct");
            }

            object array;
            if (value is JArray)
            {
                var listType = typeof(IEnumerable<>).MakeGenericType(visitedExpression.Type);
                array = ((JArray)value).ToObject(listType);
            }
            else
            {
                array = value;
            }

            var anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
            anyMethod = anyMethod.MakeGenericMethod(childParam.Type);

            Expression exp = null;
            foreach (var item in (IEnumerable)array)
            {
                var equal = CreateOperationExpression(visitedExpression, ConditionRuleOperator.equal, item);
                Expression anyExp = Expression.Call(anyMethod, arrayExpression, Expression.Lambda(equal, childParam));

                if (@operator == ConditionRuleOperator.excludeAll)
                {
                    anyExp = Expression.Not(anyExp);
                }

                if (exp == null)
                {
                    exp = anyExp;
                }
                else
                {
                    exp = Expression.AndAlso(exp, anyExp);
                }

            }


            return exp;
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

        private static bool IsClass(Type type)
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

            bool isNullable = property.Type.IsNullable();

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
                        // Specific case of TimeSpan Stored as "\00:22:00"\""
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
                        try
                        {
                            value = DateTime.Parse(value.ToString());
                        }
                        catch (Exception)
                        {
                            throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.InvalidValue, $"Unable to cast value {value} to date, please provide the right format yyyy-mm-dd");
                        }
                    }
                }
                // Case of a standard value nullable
                else if (isNullable)
                {
                    value = Nullable.GetUnderlyingType(property.Type).GetValue(value);
                }
                else
                {
                    value = property.Type.GetValue(value);
                }

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

            if (isNullable && op != ConditionRuleOperator.isNotNull && op != ConditionRuleOperator.isNull)
            {
                expression = Expression.AndAlso(Expression.Property(inputProperty, "HasValue"), expression);
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
