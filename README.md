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
| operator   | enum (equal,notEqual, lessThan, lessThanInclusive,greaterThan, greaterThanInclusive,in,notIn, contains, doesNotContains, isNull, isNotNull) **default:equal** | The type of method used for comparing values                                                                                                |
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
- isEmpty

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