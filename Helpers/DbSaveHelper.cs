using BE_ZSM.Contexts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Helpers
{
    public class DbSaveHelper
    {
        private readonly AppDbContext _context;

        public DbSaveHelper(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> TrySaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException ex)
            {
                return GetDatabaseErrorMessage(ex);
            }
        }

        private static string GetDatabaseErrorMessage(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlException)
            {
                return sqlException.Number switch
                {
                    547 => "Database constraint violated.",
                    2601 => "Duplicate data exists in the database.",
                    2627 => "Duplicate data exists in the database.",
                    515 => "A required value is missing.",
                    _ => $"Database update failed: {sqlException.Message}"
                };
            }

            return $"Database update failed: {ex.InnerException?.Message ?? ex.Message}";
        }
    }
}
