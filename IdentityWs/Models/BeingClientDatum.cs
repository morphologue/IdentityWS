using System.ComponentModel.DataAnnotations;

namespace IdentityWs.Models
{
    public class BeingClientDatum
    {
        public int BeingClientDatumID { get; set; }
        public int BeingClientID { get; set; }
        [Required]
        public string Key { get; set; }
        [Required]
        public string Value { get; set; }
        public BeingClient BeingClient { get; set; }
    }
}
