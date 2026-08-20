using BE_ZSM.Entities;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Extensions;

public static class RecordQueryExtensions
{
    public static IQueryable<Record> IncludeDetails(
        this IQueryable<Record> query)
    {
        return query
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle);
    }
}