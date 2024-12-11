using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HackTownBack.Models
{
    public class UserRequest
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required, MaxLength(50)]
        public string EventType { get; set; }

        public int PeopleCount { get; set; }

        public DateTime? EventTime { get; set; }

        public int CostTier { get; set; }

        public DateTime RequestTime { get; set; } = DateTime.Now;

        [ForeignKey("EventRoute")]
        public int? EventRoutesId { get; set; }

        public string Response { get; set; }

        // Навігаційні властивості
        public User User { get; set; }

        public EventRoute EventRoute { get; set; }
    }
    public class UserRequestDto
    {
        public int UserId { get; set; }
        public string EventType { get; set; }
        public int PeopleCount { get; set; }
        public DateTime? EventTime { get; set; } // Робимо необов’язковим
        public int CostTier { get; set; }
        public string? Coords { get; set; }
    }


}
