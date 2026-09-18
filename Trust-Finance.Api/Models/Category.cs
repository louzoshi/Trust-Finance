using System.Text.Json.Serialization;

namespace TF.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public int UserId { get; set; }

        [JsonIgnore] // the owner is implicit in the request; never serialize it back
        public User User { get; set; } = null!;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
