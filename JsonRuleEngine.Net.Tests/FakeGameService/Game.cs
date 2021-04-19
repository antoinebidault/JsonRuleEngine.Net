using System;

namespace JsonRuleEngine.Net.Tests
{
    public class Game
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool BoolValue { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Name { get; set; }
        public double Price { get; set; }
        public string Category { get; set; } = "Action";
    }
}
