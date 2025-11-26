using System.ComponentModel.DataAnnotations;

namespace KlodTattooWeb.Models
{
    public class TattooStyle
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public ICollection<PortfolioItem> PortfolioItems { get; set; } = new List<PortfolioItem>();
    }
}
