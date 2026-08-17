using BE_ZSM.Entities;

namespace BE_ZSM.Repositories.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();

    Task<User?> GetByIdAsync(int id);

    Task<User?> GetByUsernameAsync(string username);

    Task<bool> ExistsByUsernameAsync(string username);

    Task<bool> ExistsByEmailAsync(
        string email,
        int? excludeUserId = null);

    Task AddAsync(User user);

    void Delete(User user);

    Task SaveChangesAsync();
}