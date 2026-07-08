using System.Text.Json.Serialization;

namespace TF.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        [JsonIgnore] // never expose the hash in API responses
        public string PasswordHash { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}