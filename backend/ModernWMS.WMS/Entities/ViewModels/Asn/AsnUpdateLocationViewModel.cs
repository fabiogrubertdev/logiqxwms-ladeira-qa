/*
 * date：2025-11-13
 * developer：Manus AI
 */
using System.ComponentModel.DataAnnotations;

namespace ModernWMS.WMS.Entities.ViewModels
{
    /// <summary>
    /// Update ASN goods location name
    /// </summary>
    public class AsnUpdateLocationViewModel
    {
        /// <summary>
        /// asn_id
        /// </summary>
        [Required(ErrorMessage = "Required")]
        public int asn_id { get; set; }

        /// <summary>
        /// goods_location_name
        /// </summary>
        [Required(ErrorMessage = "Required")]
        [MaxLength(50, ErrorMessage = "MaxLength")]
        public string goods_location_name { get; set; } = string.Empty;
    }
}
