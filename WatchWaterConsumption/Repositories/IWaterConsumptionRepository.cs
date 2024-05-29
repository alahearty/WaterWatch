using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WatchWaterConsumption.Models;

namespace WatchWaterConsumption.Repositories
{
    public interface IWaterConsumptionRepository
    {
        Task<IEnumerable<WaterConsumption>> GetAll();
        Task<IEnumerable<WaterConsumption>> GetTopTenConsumers();
    }
}