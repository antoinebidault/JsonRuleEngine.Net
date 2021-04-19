using System.ComponentModel.DataAnnotations;

namespace JsonRuleEngine.Net.Tests
{
    public class Review
    {
        [Key]
        public int Id { get; set; }
        public string Text { get; set; }
    }
}
