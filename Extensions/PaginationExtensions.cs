using AutoMapper;
using BE_ZSM.Responses;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Extensions
{
    public static class PaginationExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;
            if (pageSize > 100) pageSize = 100;

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            };
        }
        public static async Task<PagedResult<TDestination>> ToPagedResultAsync<TSource, TDestination>(this IQueryable<TSource> query, int page, int pageSize, IMapper mapper)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            if (pageSize > 100) pageSize = 100;

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<TDestination>
            {
                Items = mapper.Map<List<TDestination>>(items),
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            };
        }
    }
}
