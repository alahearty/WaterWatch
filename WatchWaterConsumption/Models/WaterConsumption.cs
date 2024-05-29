using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WatchWaterConsumption.Models
{
    public class WaterConsumption
    {
        public int Id { get; set; }
        public string? Neighbourhood { get; set; }
        public string? SuburbGroup { get; set; }
        public int? AverageMonthlyKL { get; set; }
        public string? Stroke { get; set; }
        public string? StrokeWidth { get; set; }
        public string? StrokeOpacity { get; set; }
        public string? Fill { get; set; }
        public string? FillOpacity { get; set; }
        public string? Coordinates { get; set; }
    }
}