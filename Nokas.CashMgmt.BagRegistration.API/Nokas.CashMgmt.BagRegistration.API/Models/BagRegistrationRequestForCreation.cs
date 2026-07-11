using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    [XmlRoot("BagRegistrationRequest")]
    public class BagRegistrationRequestForCreation
    {

        [Required]
        [JsonPropertyName("bagRegistration")]
        [XmlElement("BagRegistration")]
        public BagRegistration BagRegistration { get; set; }
    }
}
