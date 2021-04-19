

[![Build status](https://ci.appveyor.com/api/projects/status/r430k8vb29wjjsfd?svg=true)](https://ci.appveyor.com/project/antoinebidault/jsonruleengine-net)
![Nuget](https://img.shields.io/nuget/v/JsonRuleEngine.Net)

# JsonRuleEngine.Net

A simple C# rule engine parser and evaluator using a simple json format.
This lib is inspired by the [json rules engine](https://github.com/cachecontrol/json-rules-engine).
We plan to use it in production in the [Dastra](https://www.dastra.eu) filtering engine 

# Purpose
In some case you'll need to store some complex conditions object in database. The purpose of this library is to provide a simple way to store and transform to linq Expression tree nested conditional rules stored in json in database, filesystem... 

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
    // Save it in DB or whatever
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
string ruleJson = "{\"field\": \"Name\",\"operator\": \"equal\",\"value\": \"Assassin's creed\" }";

Game objectToTest = new Game() { 
    Name = "Assassin's creed"
};

bool result = JsonRuleEngine.Evaluate(objectToTest, ruleJson);

return result; // this must display "True"
```

## For filtering a list using an expression
The expression parsed will work with LinqToSql query with EntityFramework Core.
```CSharp
string ruleJson = "{}"
var expression = JsonRuleEngine.ParseExpression<Game>(ruleJson);
var datas = new List<Game>() {
    new Game() {
        Name = "test"
    }
};

// Works with LinqToSql queries
var list = datas.Where(expression).ToList();
```

# The nested rules object
## ConditionRuleSet
|Field name| Type| Description |
|--|--|--|
|separator|enum (Or, And) **optional**| The type of condition rules  |
|field|string **optional**| The name of the field used for filtering (Camel sensitive). If the rules properties contains no element **this field must be set**  |
|operator|enum (equal,notEqual,  lessThan,lessThanInclusive,greaterThan, greaterThanInclusive,in,notIn, contains,  doesNotContains) **default:equal**| The type of method used for comparing values |
|value|object **optional, default:null**| The string value, the number or the object used for egality comparison. In case, the in operator is used, this **must be a list of string** |
|rules| List of ConditionRuleSet **optional, default: null** | The nested rules contained in the group  |
