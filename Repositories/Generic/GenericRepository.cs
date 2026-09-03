using BE_ZSM.Contexts;
using BE_ZSM.Repositories.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

public class GenericRepository<T>
    : IGenericRepository<T>
    where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public IQueryable<T> All()
    {
        return _dbSet;
    }

    public IQueryable<T> Where(
        Expression<Func<T, bool>> predicate)
    {
        return _dbSet.Where(predicate);
    }

    public IQueryable<T> WhereInclude(
        Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] includeProperties)
    {
        IQueryable<T> query = _dbSet.Where(predicate);

        foreach (var includeProperty in includeProperties)
        {
            query = query.Include(includeProperty);
        }

        return query;
    }

    public async Task<T?> FindAsync(
        Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public async Task<T> CreateAsync(T item)
    {
        await _dbSet.AddAsync(item);
        return item;
    }

    public async Task CreateRangeAsync(IEnumerable<T> items)
    {
        await _dbSet.AddRangeAsync(items);
    }

    public async Task<T> UpdateAsync(T item)
    {
        _dbSet.Update(item);
        return await Task.FromResult(item);
    }

    public async Task UpdateRangeAsync(IEnumerable<T> items)
    {
        _dbSet.UpdateRange(items);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(T item)
    {
        _dbSet.Remove(item);
        await Task.CompletedTask;
    }

    public void DeleteRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }
    public void SetOriginalValue<TProperty>(T entity, Expression<Func<T, TProperty>> property, TProperty value)
    {
        _context.Entry(entity)
            .Property(property)
            .OriginalValue = value;
    }
}