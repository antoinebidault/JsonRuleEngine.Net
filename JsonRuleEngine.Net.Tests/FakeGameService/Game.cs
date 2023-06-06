using System;
using System.Collections.Generic;

namespace JsonRuleEngine.Net.Tests
{
    public class Game
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool BoolValue { get; set; }
        public DateTime? Date { get; set; } = DateTime.UtcNow;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public GameType Type { get; set; } = GameType.Action;
        public GameState? State { get; set; } 
        public string Name { get; set; }
        public double Price { get; set; }
        public int? Stock { get; set; }
        public string Category { get; set; } = "Action";
        public Editor Editor { get; set; } = new Editor();
        public IEnumerable<string> Tags { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new Dictionary<string, object>();
        public IEnumerable<Review> Reviews { get; set; } = new List<Review>();
    }

    public enum GameType
    {
        Action,
        RPG,
        CityBuilder
    }

    public enum GameState
    {
        New,
        Active,
        Removed
    }
}

