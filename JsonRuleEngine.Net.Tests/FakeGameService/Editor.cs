using System.ComponentModel.DataAnnotations;

namespace JsonRuleEngine.Net.Tests
{
    public class Editor
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public Company Company { get; set; }
    }
}
