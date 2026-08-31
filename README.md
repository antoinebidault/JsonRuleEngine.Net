[![Build status](https://ci.appveyor.com/api/projects/status/r430k8vb29wjjsfd?svg=true)](https://ci.appveyor.com/project/antoinebidault/jsonruleengine-net)
[![Nuget](https://img.shields.io/nuget/v/JsonRuleEngine.Net)](https://www.nuget.org/packages/JsonRuleEngine.Net/)
[![codecov](https://codecov.io/gh/antoinebidault/JsonRuleEngine.Net/branch/master/graph/badge.svg?token=3KK1MJAW46)](https://codecov.io/gh/antoinebidault/JsonRuleEngine.Net)

![Logo](/JsonRuleEngine.Net/JsonRuleEngine.Net.png)

# JsonRuleEngine.Net

A simple C# Asp.Net Core rule engine parser and evaluator using a simple json format.



lib is inspired by the [json rules engine](https://github.com/cachecontrol/json-rules-engine).
We are using it in production in the [Dastra](https://www.dastra.eu) complex table filtering engine and it works like a charm :).

# Purpose

In some case you'll need to store some complex conditions objects in database. The purpose of this library is to provide a simple way to store and transform to linq Expression tree nested conditional rules stored in a simple json format you can save in database, filesystem... Out of the box, you'll be able to evaluate it as a Linq Expression and use it for applying filters in Entity Framework.

# Json format of queries

Here is a basic JSON sample that represents rules

```javascript
{
  "separator": "And",
  "rules": [
    {
      "separator": "Or",
      "rules": [
        {
          "field": "Name",
          "operator": "equal",
          "value": "Assassin's creed"
        },
        {
          "field": "Name",
          "operator": "equal",
          "value": "Data"
        }
      ]
    },
    {
      "field": "Category",
      "operator": "in",
      "value": [
        "Action",
        "Adventure"
      ]
    },
    {
      "field": "Price",
      "operator": "greaterThan",
      "value": 5
    }
  ]
}
```

You can post it to a simple controller using the ConditionRuleSet class

```CSharp
[HttpPost]
public IActionResult PostRules([FromBody] ConditionRuleSet rules) {
    // Then, save it in DB or whatever
    if (ModelState.IsValid) {
	    _db.Add(rules);
	    _db.SaveChanges();
    }
}

```

# Simple use

## Installation

You need to install the nuget library

```
install-package JsonRuleEngine.Net
```

## For evaluating a rule with a single object

```CSharp
// Simple json rule definition
string ruleJson = "{\"field\": \"Name\",\"operator\": \"equal\",\"value\": \"Assassin's creed\" }";

Game objectToTest = new Game() {
    Name = "Assassin's creed"
};

bool result = JsonRuleEngine.Evaluate(objectToTest, ruleJson);

return result; // this must display "True"
```

## For evaluating a rule with return value

```CSharp
// Simple json rule definition
string ruleJson = "{\"field\": \"Name\",\"operator\": \"equal\",\"value\": \"Assassin's creed\", \"returnValue\":{\"type\": System.String\", \"value\": \"Good game\" } }";

Game objectToTest = new Game() {
    Name = "Assassin's creed"
};

string result = JsonRuleEngine.Evaluate<Game, String>(objectToTest, ruleJson);

return result; // this must display "Good Game"
```

## Support of navigation properties

If you have complex models with nested list or object, you are able to apply filters on them using the dot (.) separator on field.

Example of model with a nested list and object :

```CSharp
public class Game {
    public Guid Id { get; set; }
    public Author Author { get; set; }
    public IEnumerable<Review> Reviews { get; set; }
}

public class Author {
    public Guid Id { get; set; }
    public string Name { get; set; }
}

public class Reviews {
    public Guid Id { get; set; }
    public int Score { get; set; }
}
```

If you want all the game with author named "John Doe" and one review with a score of 3 or 5

```CSharp
string ruleJson = "{ \"rules\": [ " +
   " {\"field\": \"Author.Name\",\"operator\": \"equal\",\"value\": \"John Doe\" }, " +
   " {\"field\": \"Reviews.Score\",\"operator\": \"in\",\"value\": [3,5] } " +
" ]";

Game objectToTest = new Game() {
    Name = "Assassin's creed",
    Author = new Author(){
        Name = "John Doe"
    },
    Reviews = new [] {
        new Review() {
            Score = 3
        },
        new Review() {
            Score = 4
        },
        new Review() {
            Score = 1
        }
    }
};

bool result = JsonRuleEngine.Evaluate(objectToTest, ruleJson);

Assert.True(result)
```

Limitations : for nested list it works only with one level.

## For filtering a list using an expression

The expression parsed will work with LinqToSql query with EntityFramework Core.

```CSharp
string ruleJson = ""{\"field\": \"Name\",\"operator\": \"notEqual\",\"value\": \"test\" }"
var expression = JsonRuleEngine.ParseExpression<Game>(ruleJson);
var datas = new List<Game>() {
    new Game() {
        Name = "Assassin's Creed"
    }
};

// Works with EF Core LinqToSql queries
var list = datas.Where(expression).ToList();

Assert.Equal(list.Count(), 1);
```

## Entity Framework Core support

```CSharp
string ruleJson = ""{\"field\": \"Name\",\"operator\": \"notEqual\",\"value\": \"test\" }"
var expression = JsonRuleEngine.ParseExpression<Game>(ruleJson);

var list = _db.Games.Where(expression).ToList();
```

# The nested rules object / classname : ConditionRuleSet

## ConditionRuleSet

| Field name | Type                                                                                                                                                          | Description                                                                                                                                 |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| separator  | enum (Or, And) **optional**                                                                                                                                   | The type of condition rules                                                                                                                 |
| field      | string **optional**                                                                                                                                           | The name of the field used for filtering (Camel sensitive). If the rules properties contains no element **this field must be set**          |
| operator   | enum (equal,notEqual, lessThan, lessThanInclusive,greaterThan, greaterThanInclusive,in,notIn, contains, doesNotContains, isNull, isNotNull, isEmpty, isNotEmpty, includeAll, excludeAll, regex) **default:equal** | The type of method used for comparing values                                                                                                |
| value      | object **optional, default:null**                                                                                                                             | The string value, the number or the object used for egality comparison. In case, the in operator is used, this **must be a list of string** |
| rules      | List of ConditionRuleSet **optional, default: null**                                                                                                          | The nested rules contained in the group                                                                                                     |

## Supported operators

Here is the list of supported operators :

- equal,
- notEqual,
- lessThan,
- lessThanInclusive,
- greaterThan,
- greaterThanInclusive,
- in,
- notIn,
- contains,
- doesNotContains,
- isNull,
- isNotNull,
- isEmpty,
- includeAll (new) : match all elements with the condition
- excludeAll (new) : does not match all elements within the condition
- regex (new) : regular expression match, see below

### The regex operator

```json
{ "field": "Name", "operator": "regex", "value": "^GTA [IV]+$" }
```

- applies to **string fields only** (including navigation properties, collections and dictionary values),
  any other field type is rejected at parse time with an `InvalidValue` error,
- the value is the .NET regex pattern, validated at parse time. Matching is case sensitive,
  use an inline flag like `(?i)` for case insensitivity,
- a null field value never matches,
- matching is evaluated with a 1 second timeout, so a hostile pattern coming from client
  provided rules cannot hang the evaluation (ReDoS),
- it is evaluated in memory : most EF providers cannot translate `Regex.IsMatch` to SQL,
  so use it with `Evaluate` rather than inside an EF Core query.


## Support of dictionary objects (since 1.14.0)

The library now supports For dynamic objects like Dictionary<string, object>
This will not work with EF Core (SQL)

```CSharp
 var dict = new Dictionary<string, object>() {
    {"testvariable", "test" },
    {"1235", "ok2" }
};
bool result = JsonRuleEngine.Evaluate(dict, new ConditionRuleSet() { Field = "1234", Operator = ConditionRuleOperator.isNotNull });
Assert.True(result); // Return true
```


## New feature : EvaluationOptions (since 1.0.95)

You can now override the behavior of a client specified property. For example, you want to map from a more user friendly name like "EditorName" to "model.Editor.Name"

Here is a simple sample
```CSharp
   var evaluateOptions = new EvaluateOptions<Game>();
    evaluateOptions.ForProperty("EditorName", c => c.Editor.Name);

    var conditions = new ConditionRuleSet()
    {
        Rules = new[]
        {
                new ConditionRuleSet() { Field = "EditorName", Operator = ConditionRuleOperator.equal, Value = "Jean-Marc" },
        }
    };

    var expectedResult = FakeGameService.GetDatas().Count(m => m.DateCreation < date && m.Editor.Name == "Jean-Marc");
    var result = FakeGameService.GetDatas()
            .Where(m => JsonRuleEngine.Evaluate<Game>(m, conditions, evaluateOptions))
            .ToList();
```

# Custom accessors

Both accessors are instance properties : set them on the engine instance you then use to evaluate or parse.

## CustomPropertyAccessor

Called **once per field segment** (`"Extra.InternetTLD"` gives one call for `Extra`, then one for `InternetTLD`).
Return `null` to let the engine resolve the segment as usual, or an expression to replace the member access.
`ctx.ValueCompared` can be rewritten, and the engine still applies the operator on the returned expression.

```CSharp
var engine = new JsonRuleEngine();
engine.CustomPropertyAccessor = (ctx) =>
{
    // ctx.Expression is the expression built so far, ctx.MemberName the current segment
    if (ctx.Expression != null && ctx.Expression.Type == typeof(Dictionary<string, object>))
    {
        return Expression.Call(MyDictionaryAccessMethod, ctx.Expression, Expression.Constant(ctx.MemberName));
    }

    return null;
};
```

## CustomConditionRuleSetAccessor (new)

A lower level hook : it receives the **complete leaf `ConditionRuleSet`** instead of a single field segment, so the
full dotted field, the operator and the value are available in one call. Use it to transpile a whole rule yourself,
typically to target a json column with EF Core.

Contract :

- return `null` : the engine handles the rule as usual,
- return a `bool` / `bool?` expression : it is used as the predicate of the rule as is,
- return any other expression : the engine applies the operator and the value of the rule on it,
  exactly as it would have done itself. `ctx.ApplyOperator(...)` does the same thing explicitly.

The claimed field **does not need to exist** on `T` : claimed fields bypass the field validation and the collection
regrouping pass, which is what makes virtual / json fields possible.

```CSharp
var engine = new JsonRuleEngine();
engine.CustomConditionRuleSetAccessor = (ctx) =>
{
    // ctx.Field is the complete path, never splitted : "Json.InternetTLD"
    if (ctx.Field != null && ctx.Field.StartsWith("Json."))
    {
        var path = ctx.Field.Substring("Json.".Length);

        // One single call, translatable by EF Core
        Expression access = Expression.Call(
            JsonValueMethod,                                                   // EF.Functions.JsonValue
            Expression.Property(ctx.InputParam, nameof(Website.ExtraInformation)),
            Expression.Constant("$." + path));

        // Let the engine apply equal / in / contains / dates / nullables ...
        return ctx.ApplyOperator(access);
    }

    return null;
};

var rules = new ConditionRuleSet() { Field = "Json.InternetTLD", Operator = ConditionRuleOperator.equal, Value = "fr" };
var query = dbContext.Websites.Where(engine.ParseExpression<Website>(rules));
```

Context provided to the accessor :

| Property | Description |
| ------------- |-------------|
| Rule | The whole leaf rule (field, operator, value, ...) |
| Field | Shortcut on `Rule.Field`, the complete dotted path |
| Operator | Operator of the rule |
| Value | Compared value, pre-filled from `Rule.Value`. Rewrite it to change the compared value |
| InputParam | The parameter the returned expression must be built on |
| InputType | Type of `InputParam` |
| IsCollectionItem | `true` when the rule is a sub rule of a collection (ex : `Reviews.Text`). `InputParam` is then the collection item, and the engine still wraps the result in a `Any()` / `All()` call |
| ApplyOperator(expression[, value]) | Apply the engine operator logic (in/notIn, contains, dates, TimeSpan, nullables ...) on your expression |

Notes :

- groups (rules with a `Separator` and inner `Rules`) are never passed to this accessor, `And` / `Or` trees are
  always built by the engine,
- this accessor has priority over the `EvaluateOptions` transformers and over `CustomPropertyAccessor`,
- it is probed once per leaf rule and must not have side effects.

# Engine internals : compiler and cache

## The compiler

Expressions are built by a single pass compiler :

- `And` / `Or` groups produce short circuiting `AndAlso` / `OrElse` expressions,
- the reflection walk detecting collections in field paths is cached,
- `CustomConditionRuleSetAccessor` is probed exactly once per leaf rule,
- the semantics of the previous engine are pinned by a golden master test suite
  (a corpus of rules paired with their equivalent LINQ predicates).

## Compiled predicate cache

`Evaluate(...)` compiles the expression to a delegate, which costs milliseconds : it used to be the dominant
cost of every call. Compiled predicates are now cached, keyed by the **content** of the rules (their json) and
the input type, so evaluating the same rules repeatedly compiles only once :

- mutating a rule object between two evaluations is always honored (the key derives from content, not identity),
- evaluations using `EvaluateOptions`, `CustomPropertyAccessor` or `CustomConditionRuleSetAccessor` never use
  the cache (the expression depends on user code the key cannot capture),
- `ParseExpression(...)` is not cached : it returns a fresh expression tree, as always.

```CSharp
var engine = new JsonRuleEngine() { UseCompiledExpressionCache = false }; // opt out
```

## Performance

Measured with the BenchmarkDotNet project in `JsonRuleEngine.Net.Benchmarks` (.NET 10, i9-11900K) :

| Scenario | Before | After | |
| ------------- |-------------|-------------|-------------|
| ParseExpression, complex rule | 11.6 µs / 15.7 KB | 9.5 µs / 12.6 KB | compiler rewrite |
| Evaluate, simple rule | 115 µs | 0.41 µs | cache, ~280x |
| Evaluate, complex rule | 836 µs | 3.9 µs | cache, ~215x |
| Evaluate, complex rule from json string | - | 0.77 µs / 128 B | cache hit skips parsing entirely |