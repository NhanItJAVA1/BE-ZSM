using BE_ZSM.Entities;
using BE_ZSM.Repositories.Generic;

namespace BE_ZSM.Repositories.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdWithRoleAsync(int id);
    Task<List<User>> GetAllWithRoleAsync();
    Task<bool> ExistsByUsernameAsync(string username);

    Task<bool> ExistsByEmailAsync(
        string email,
        int? excludeUserId = null);
}