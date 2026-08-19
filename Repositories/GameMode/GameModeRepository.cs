using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;

public class GameModeRepository : GenericRepository<GameMode>, IGameModeRepository
{

    public GameModeRepository(
        AppDbContext context) : base(context)
    {
    }
}