using AutoMapper;
using BE_ZSM.DTOs.GameModes;
using BE_ZSM.DTOs.Maps;
using BE_ZSM.DTOs.Records;
using BE_ZSM.DTOs.Todos;
using BE_ZSM.DTOs.Todos.Categories;
using BE_ZSM.DTOs.Users;
using BE_ZSM.DTOs.Vehicles;
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
                opt => opt.MapFrom(src => src.Role.Name.ToString())
            );

        CreateMap<CreateVehicleDto, Vehicle>();
        CreateMap<UpdateVehicleDto, Vehicle>();
        CreateMap<Vehicle, VehicleResponseDto>();

        CreateMap<Record, RecordResponseDto>()
            .ForMember(
                dest => dest.FinishTime,
                opt => opt.MapFrom(src => src.FinishTime.TotalSeconds)
            );
        CreateMap<CreateRecordDto, Record>();
        CreateMap<User, UserMinimalDto>();
        CreateMap<Map, MapMinimalDto>()
            .ForMember(
                dest => dest.Rate,
                opt => opt.MapFrom(src => src.Rate.ToString())
            );
        CreateMap<Vehicle, VehicleMinimalDto>();
        CreateMap<GameMode, GameModeMinimalDto>();
        
        CreateMap<Map, MapResponseDto>();
        CreateMap<CreateMapDto, Map>();
        CreateMap<UpdateMapDto, Map>();

        CreateMap<GameMode, GameModeResponseDto>();
        CreateMap<CreateGameModeDto, GameMode>();
        CreateMap<UpdateGameModeDto, GameMode>();

        CreateMap<Todo, TodoDto>()
           .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
           .ForMember(dest => dest.IsOverdue, opt => opt.MapFrom(src => src.DueDate.HasValue && src.DueDate.Value < DateTime.UtcNow && src.Status != TodoStatus.Done))
           .ForMember(dest => dest.IsCompletedLate, opt => opt.MapFrom(src => src.CompletedAt.HasValue && src.DueDate.HasValue && src.CompletedAt.Value > src.DueDate.Value));

        //CreateMap<TodoRequestDto, Todo>()
        //    .ForMember(dest => dest.Id,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.UserId,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.User,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.Status,
        //        opt => opt.MapFrom(_ => TodoStatus.Todo))
        //    .ForMember(dest => dest.CreatedAt,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.UpdatedAt,
        //        opt => opt.Ignore());

        //CreateMap<TodoRequestDto, Todo>()
        //    .ForMember(dest => dest.Id,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.UserId,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.User,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.CreatedAt,
        //        opt => opt.Ignore())
        //    .ForMember(dest => dest.UpdatedAt,
        //        opt => opt.MapFrom(_ => DateTime.UtcNow));

        CreateMap<SaveTodoDto, Todo>()
            .ForMember(dest => dest.Id,
                opt => opt.Ignore())
            .ForMember(dest => dest.UserId,
                opt => opt.Ignore())
            .ForMember(dest => dest.User,
                opt => opt.Ignore())
            .ForMember(dest => dest.Status,
                opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt,
                opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt,
                opt => opt.Ignore())
            .ForMember(dest => dest.CompletedAt,
                opt => opt.Ignore())
            .ForMember(dest => dest.Category,
                opt => opt.Ignore())
            .ForMember(dest => dest.Activities,
                opt => opt.Ignore());

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
