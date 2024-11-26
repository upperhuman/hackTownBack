using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HackTownBack.Models
{
    public class Location
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("EventRoute")]
        public int? EventId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(255)]
        public string Address { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public string Description { get; set; }

        [Required, MaxLength(50)]
        public string Type { get; set; }

        [Required]
        public int StepNumber { get; set; }

        public EventRoute EventRoute { get; set; }
    }
    public class LocationDto
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
    }

}
