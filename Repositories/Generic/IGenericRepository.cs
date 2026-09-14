using System.Linq.Expressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BE_ZSM.Repositories.Generic
{
    public interface IGenericRepository<T> where T : class
    {        
        IQueryable<T> All();
        IQueryable<T> Where(Expression<Func<T, bool>> predicate);
        IQueryable<T> WhereInclude(Expression<Func<T, bool>> predicate, params Expression<Func<T, System.Object>>[] includeProperties);

        Task<T?> FindAsync (Expression<Func<T, bool>> predicate);
        Task<T> CreateAsync(T item);
        Task CreateRangeAsync(IEnumerable<T> items);

        Task<T> UpdateAsync(T item);
        void UpdateRange(IEnumerable<T> items);

        Task DeleteAsync(T item);
        Task DeleteByIdAsync(object id );
        void DeleteRangeAsync(IEnumerable<T> entities);
        Task DeleteAsync(Expression<Func<T, bool>> predicate);

        void SetOriginalValue<TProperty>(T entity, Expression<Func<T, TProperty>> property, TProperty value);

    }
}
