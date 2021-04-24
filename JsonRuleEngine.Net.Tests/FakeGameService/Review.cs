using System.ComponentModel.DataAnnotations;

namespace JsonRuleEngine.Net.Tests
{
    public class Review
    {
        [Key]
        public int Id { get; set; }
        public string Text { get; set; }
        public Author Author { get; set; } = new Author();
    }

    public class Author
    {
        public string Name { get; set; }
    }
}
