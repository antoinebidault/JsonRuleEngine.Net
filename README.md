# JsonRuleEngine.Net
A json-rule-engine simple porting to Asp.Net Core
(https://github.com/cachecontrol/json-rules-engine)[https://github.com/cachecontrol/json-rules-engine]

# Json format 
Here is the basic JSON docs used to stores the rules
YOu can store it into your favorite database system (SQL server, Postgresql, mongodb or whatever...)
```javascript
{
   any: [{
        all: [{
          fact: 'gameDuration',
          operator: 'equal',
          value: 40
        }, {
          fact: 'personalFoulCount',
          operator: 'greaterThanInclusive',
          value: 5
        }]
      }, {
        all: [{
          fact: 'gameDuration',
          operator: 'equal',
          value: 48
        }, {
          fact: 'personalFoulCount',
          operator: 'greaterThanInclusive',
          value: 6
        }]
      }]

}


# Simple use

```CSharp
var expression = JsonRuleEngine.ParseExpression<Game>(rules);
var datas = FakeGameService.GetDatas();
var list = datas.Where(expression).ToList();
```

# Simple uses


