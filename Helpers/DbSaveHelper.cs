using BE_ZSM.Contexts;
using BE_ZSM.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Helpers;

public class DbSaveHelper
{
    private readonly AppDbContext _context;

    public DbSaveHelper(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw ConvertDatabaseException(ex);
        }
    }

    private static Exception ConvertDatabaseException(
        DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlException)
        {
            return sqlException.Number switch
            {
                547 => new BadRequestException(
                    "Database constraint violated.",
                    "DATABASE_CONSTRAINT_VIOLATED"),

                2601 => new BadRequestException(
                    "Duplicate data exists in the database.",
                    "DUPLICATE_DATA"),

                2627 => new BadRequestException(
                    "Duplicate data exists in the database.",
                    "DUPLICATE_DATA"),

                515 => new BadRequestException(
                    "A required value is missing.",
                    "REQUIRED_VALUE_MISSING"),

                _ => new AppException(
                    "Database update failed.",
                    500,
                    "DATABASE_UPDATE_FAILED")
            };
        }

        return new AppException(
            "Database update failed.",
            500,
            "DATABASE_UPDATE_FAILED");
    }
}