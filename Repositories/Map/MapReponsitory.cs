using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using MapEntity = BE_ZSM.Entities.Map;

namespace BE_ZSM.Repositories;

public class MapRepository : GenericRepository<Map>, IMapRepository
{
    public MapRepository(AppDbContext context) : base(context)
    {
    }
}