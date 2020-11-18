using System;
using System.Collections.Generic;
using System.Text;

namespace Correios.NET.Models
{
    public class DeliveryPrice
    {
        public Address Origin { get; set; }
        public Address Destination { get; set; }

        public string Mode { get; set; }
        public decimal Price { get; set; }
        public int Days { get; set; }

    }
}
