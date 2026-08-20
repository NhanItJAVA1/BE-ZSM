using BE_ZSM.Repositories.Generic;

public interface IUnitOfWork
{
    IGenericRepository<TEntity> GetRepository<TEntity>()
        where TEntity : class;

    Task<int> SaveChangesAsync();
}