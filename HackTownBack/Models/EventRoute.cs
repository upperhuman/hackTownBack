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

        public ICollection<Location> Locations { get; set; } = new List<Location>();

        public ICollection<UserRequest> UserRequests { get; set; } = new List<UserRequest>();
    }
    public class RouteResponse
    {
        public int RouteId { get; set; }
        public string RouteName { get; set; }
        public BudgetBreakdown BudgetBreakdown { get; set; }
        public List<LocationDto> Locations { get; set; }
    }
    public class RouteApiResponce
    {
        public int RouteId { get; set; }
        public string RouteName { get; set; }
    }
}
