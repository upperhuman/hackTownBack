using System.ComponentModel.DataAnnotations;

namespace HackTownBack.Models
{
    public class EventRoute
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string RouteName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int StepsCount { get; set; }

        public ICollection<Location> Locations { get; set; }

        public ICollection<UserRequest> UserRequests { get; set; }
    }
}
