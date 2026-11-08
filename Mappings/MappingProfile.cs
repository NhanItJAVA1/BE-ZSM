using AutoMapper;
using BE_ZSM.DTOs.Todos;
using BE_ZSM.DTOs.Todos.Categories;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<RegisterUserDto, User>();
        CreateMap<UpdateUserDto, User>();
        CreateMap<User, UserResponseDto>()
            .ForMember(
                dest => dest.Role,
                opt => opt.MapFrom(src => src.Role.Name))
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.UserRoles
                .Select(ur => ur.Role.Name)
                .Distinct()
                .OrderBy(name => name)));

        CreateMap<Todo, TodoDto>()
           .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
           .ForMember(dest => dest.IsOverdue, opt => opt.MapFrom(src => src.DueDate.HasValue && src.DueDate.Value < DateTime.UtcNow && src.Status != TodoStatus.Done))
           .ForMember(dest => dest.IsCompletedLate, opt => opt.MapFrom(src => src.CompletedAt.HasValue && src.DueDate.HasValue && src.CompletedAt.Value > src.DueDate.Value));

        CreateMap<SaveTodoDto, Todo>()
            .ForMember(x => x.RowVersion, opt => opt.Ignore())
            .ForMember(x => x.UserId, opt => opt.Ignore())
            .ForMember(x => x.User, opt => opt.Ignore())
            .ForMember(x => x.UpdatedAt, opt => opt.Ignore())
            .ForMember(x => x.CompletedAt, opt => opt.Ignore())
            .ForMember(x => x.Category, opt => opt.Ignore())
            .ForMember(x => x.Activities, opt => opt.Ignore());
        
        CreateMap<SaveTodoDto, Todo>()
            .ForMember(x => x.RowVersion, opt => opt.Ignore())
            .ForMember(x => x.UserId, opt => opt.Ignore())
            .ForMember(x => x.User, opt => opt.Ignore())
            .ForMember(x => x.UpdatedAt, opt => opt.Ignore())
            .ForMember(x => x.CompletedAt, opt => opt.Ignore())
            .ForMember(x => x.Category, opt => opt.Ignore())
            .ForMember(x => x.Activities, opt => opt.Ignore())
            .AfterMap((src, dest, context) =>
            {
                dest.UserId = (int)context.Items["UserId"];
                if (src.Id.HasValue)
                    dest.UpdatedAt = DateTime.UtcNow;
            });

        CreateMap<TodoCategory, TodoCategoryDto>();

        CreateMap<CreateTodoCategoryDto, TodoCategory>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Todos, opt => opt.Ignore());

        CreateMap<UpdateTodoCategoryDto, TodoCategory>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Todos, opt => opt.Ignore());

        CreateMap<TodoActivity, TodoActivityDto>();
    }
}

