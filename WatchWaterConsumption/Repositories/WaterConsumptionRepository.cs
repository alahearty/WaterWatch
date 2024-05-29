using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatchWaterConsumption.Models;

namespace WatchWaterConsumption.Repositories
{
    public class WaterConsumptionRepository : IWaterConsumptionRepository
    {
        private readonly DatabaseContext _context;
        private readonly IWebHostEnvironment _env;

        public WaterConsumptionRepository(DatabaseContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IEnumerable<WaterConsumption>> GetAll()
        {
            await SaveDataAsync();
            return await _context.Consumptions.ToListAsync();
        }

        public async Task<IEnumerable<WaterConsumption>> GetTopTenConsumers()
        {
            return await _context.Consumptions
                .OrderByDescending(avgKL => avgKL.AverageMonthlyKL)
                .Take(10)
                .ToListAsync();
        }

        private async Task SaveDataAsync()
        {
            if (_context.Consumptions.Any())
            {
                Console.WriteLine("Data Loaded");
                return;
            }

            Console.WriteLine("No Data");

            string geoJSONPath = Path.Combine(_env.ContentRootPath, "map.geojson");
            try
            {
                string geoJSON = await File.ReadAllTextAsync(geoJSONPath);
                var jsonObj = JsonConvert.DeserializeObject<GeoJsonObject>(geoJSON);

                if (jsonObj?.Features != null)
                {
                    foreach (var feature in jsonObj.Features)
                    {
                        var properties = feature.Properties;

                        string str_neighbourhood = properties.Neighbourhood;
                        string str_suburb_group = properties.SuburbGroup;
                        string str_avgMonthlyKL = properties.AverageMonthlyKL;
                        string str_stroke = properties.Stroke;
                        string str_strokeWidth = properties.StrokeWidth;
                        string str_stroke_opacity = properties.StrokeOpacity;
                        string str_fill = properties.Fill;
                        string str_fill_opacity = properties.FillOpacity;
                        string str_geometry = JsonConvert.SerializeObject(feature.Geometry.Coordinates);

                        int? avgMonthlyKL = null;
                        if (!string.IsNullOrEmpty(str_avgMonthlyKL))
                        {
                            string conv_avgMthlyKl = str_avgMonthlyKL.Replace(".0", "");
                            if (int.TryParse(conv_avgMthlyKl, out int avgMthlyKl))
                            {
                                avgMonthlyKL = avgMthlyKl;
                            }
                            else
                            {
                                Console.WriteLine($"Failed to parse average monthly consumption: {str_avgMonthlyKL}");
                                continue;
                            }
                        }

                        WaterConsumption wc = new()
                        {
                            Neighbourhood = str_neighbourhood,
                            SuburbGroup = str_suburb_group,
                            AverageMonthlyKL = avgMonthlyKL,
                            Stroke = str_stroke,
                            StrokeWidth = str_strokeWidth,
                            StrokeOpacity = str_stroke_opacity,
                            Fill = str_fill,
                            FillOpacity = str_fill_opacity,
                            Coordinates = str_geometry
                        };

                        _context.Consumptions.Add(wc);
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    Console.WriteLine("Failed to deserialize JSON file.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while saving data: {ex.Message}");
            }
        }


    }
    // Define the GeoJsonObject classes for deserialization
    public class GeoJsonObject
    {
        [JsonProperty("features")]
        public List<Feature> Features { get; set; }
    }

    public class Feature
    {
        [JsonProperty("properties")]
        public Properties Properties { get; set; }

        [JsonProperty("geometry")]
        public Geometry Geometry { get; set; }
    }

    public class Properties
    {
        [JsonProperty("neighbourhood")]
        public string Neighbourhood { get; set; }

        [JsonProperty("suburb_group")]
        public string SuburbGroup { get; set; }

        [JsonProperty("averageMonthlyKL")]
        public string AverageMonthlyKL { get; set; }

        [JsonProperty("stroke")]
        public string Stroke { get; set; }

        [JsonProperty("stroke_width")]
        public string StrokeWidth { get; set; }

        [JsonProperty("stroke_opacity")]
        public string StrokeOpacity { get; set; }

        [JsonProperty("fill")]
        public string Fill { get; set; }

        [JsonProperty("fill_opacity")]
        public string FillOpacity { get; set; }
    }

    public class Geometry
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("coordinates")]
        public object Coordinates { get; set; }
    }

}
