# Phase 4 — Role & Permission Management APIs

## 1. API đã triển khai

| Endpoint | Permission |
|---|---|
| `GET /api/roles` | `roles.read` |
| `POST /api/roles` | `roles.create` |
| `PUT /api/roles/{id}` | `roles.update` |
| `DELETE /api/roles/{id}` | `roles.delete` |
| `GET /api/permissions` | `permissions.read` |
| `GET /api/roles/{id}/permissions` | `roles.manage-permissions` |
| `PUT /api/roles/{id}/permissions` | `roles.manage-permissions` |
| `GET /api/users/{id}/roles` | `users.manage-roles` |
| `PUT /api/users/{id}/roles` | `users.manage-roles` |

Các endpoint dùng `HasPermission`; quyền được xác minh từ database bởi authorization handler Phase 3, không dựa vào role claim JWT.

## 2. Permission codes mới

`roles.read`, `roles.create`, `roles.update`, `roles.delete`, `roles.manage-permissions`, `users.manage-roles`, `permissions.read`.

Migration chỉ gán các quyền mới cho Role `Admin`. Nếu một quyền cùng code đã tồn tại, migration không tạo bản sao; nếu phát hiện quyền quản trị RBAC đã cấp cho role khác, migration dừng để review.

## 3. Quyết định thiết kế

- Tên Role được trim, bắt buộc và giới hạn 50 ký tự. Kiểm tra trùng không phân biệt hoa/thường tại service và unique index trên computed `UPPER(Name)` để chặn cả race condition.
- ID Role mới cấp trong transaction serializable (schema hiện tại không dùng identity). Conflict/unique race trả 409.
- Không thể đổi tên hoặc xóa role mặc định `User`/`Admin`. Role đang được tham chiếu bởi `UserRoles`, `RolePermissions` hoặc legacy `Users.RoleId` không thể xóa.
- Thay permission/role dùng transaction serializable, kiểm tra tất cả ID trước khi ghi, loại trùng ID đầu vào và cập nhật quan hệ theo chênh lệch để PUT lặp lại an toàn.
- User phải giữ ít nhất một role. `Users.RoleId` được đổi cùng transaction, chỉ sang role hợp lệ; nếu role legacy cũ còn trong danh sách thì giữ nguyên, nếu không ưu tiên `User`, rồi role ID nhỏ nhất.
- “RBAC Admin” được xác định từ DB là user có đồng thời `roles.manage-permissions` và `users.manage-roles`. Không cho mất admin cuối cùng khi thay permissions, thay roles hoặc xóa user. Role mặc định `User` không được gán permission quản trị RBAC.
- API không tự gán permission cho Role mới.
- DELETE Role trả 409 khi có liên kết; không xóa dây chuyền dữ liệu.

## 4. Files thay đổi Phase 4

- Authorization: `Authorization/PermissionCodes.cs`.
- API: `Controllers/RolesController.cs`, `Controllers/PermissionsController.cs`, `Controllers/UsersController.cs`.
- DTO: `DTOs/Rbac/RoleResponseDto.cs`, `SaveRoleDto.cs`, `PermissionResponseDto.cs`, `ReplaceRolePermissionsDto.cs`, `ReplaceUserRolesDto.cs`.
- Service: `Services/Rbac/IRbacManagementService.cs`, `Services/Rbac/RbacManagementService.cs`.
- DI: `Program.cs`.
- Bảo vệ xóa user cuối cùng: `Services/User/IUserService.cs`, `Services/User/UserService.cs`.
- Model / EF: `Entities/Role.cs`, `Contexts/AppDbContext.cs`, `Migrations/AppDbContextModelSnapshot.cs`.
- Migrations: `Migrations/20261108144748_AddRoleNameNormalizedIndex.cs` (+ Designer), `Migrations/20261108144822_SeedRbacManagementPermissions.cs` (+ Designer).
- SQL review scripts: `Migrations/Phase4Rbac.sql`, `Migrations/Phase4Rbac.Down.sql`.
- Tests: `BE-ZSM.Tests/RbacManagementTests.cs`, `BE-ZSM.Tests/TestApiFactory.cs`.

## 5. Migration

Tạo hai migration theo thứ tự:

1. `20261108144748_AddRoleNameNormalizedIndex`: kiểm tra tên trùng sau `UPPER`, thêm computed column persisted và unique index; không thay Role ID hay các quan hệ.
2. `20261108144822_SeedRbacManagementPermissions`: thêm 7 permission còn thiếu và grant cho Admin. Dùng `RbacPermissionSeedHistory` để Down chỉ gỡ các row do migration tạo. Down dừng nếu permission mới được cấp thêm cho role khác sau migration.

SQL script đã sinh tại `Migrations/Phase4Rbac.sql` và `Migrations/Phase4Rbac.Down.sql`. Không chạy `database update`; chưa xác minh thực thi trên SQL Server thật hoặc disposable SQL Server. Cần kiểm tra duplicate role names hiện có trước khi áp dụng; migration sẽ fail closed nếu phát hiện.

## 6. Kết quả build và tests

- `dotnet build --no-restore`: PASS, 0 warning, 0 error.
- Authentication: PASS 11/11.
- Permission authorization: PASS 12/12.
- RBAC management: PASS 12/12.
- `dotnet ef migrations has-pending-model-changes`: PASS, không có thay đổi model chưa migration.
- Full suite: 50 passed, 41 failed, 91 total. Các lỗi tập trung ở test Todo/TodoCategory cũ: fixtures gọi `POST /api/todos` nhưng controller hiện hành không có route POST tương ứng (405); một test kỳ vọng 404 nhưng nhận 403 từ authorization. Không sửa các module/test Todo ngoài phạm vi Phase 4.
- Restore test project phát `NU1900` do không truy cập được NuGet vulnerability feed; test vẫn build/chạy được.

Integration tests RBAC dùng EF InMemory, do đó xác minh HTTP/authZ/logic ứng dụng nhưng không mô phỏng unique index, khóa serializable hoặc deadlock SQL Server.

## 7. Security review

### Vòng 1 — Backend Engineer

Đã kiểm tra DI qua service scoped, truy vấn permission/role qua EF/repository, không có vòng lặp N+1 trong thao tác thay membership. Các thay đổi nhiều liên kết chạy trong transaction; RolePermission được cập nhật theo delta. `NameNormalized` được cấu hình computed trong EF và kiểm tra migration snapshot; kiểm tra model không còn pending changes.

### Vòng 2 — Security Reviewer

- Không có đường tự cấp Admin: mọi API cần permission trong DB; User thông thường bị 403; User role không thể nhận quyền quản trị RBAC.
- Không tin role claim, user ID từ payload không được dùng làm danh tính trong authorization; user-role API là thao tác quản trị được permission bảo vệ.
- Thu hồi role/permission phản ánh từ request sau do Phase 3 query DB mỗi lần.
- Có bảo vệ admin cuối cùng (cả đổi permission, đổi role, xóa user), role mặc định, các quan hệ đang dùng và legacy FK.
- Có validate ID/duplicate, giới hạn tên và unique index chống trùng cạnh tranh.
- Kiểm soát concurrency bằng transaction serializable; xử lý SQL Server race thực tế vẫn cần xác minh ở môi trường SQL Server thử nghiệm trước production.

## 8. Vấn đề còn lại

- BLOCKER trước khi bật API: cần apply migrations lên môi trường đích theo quy trình phát hành đã duyệt; trước đó endpoint mới sẽ fail closed vì permission chưa seed.
- WARNING: test SQL Server thực tế về unique constraint, serializable conflict/deadlock, rollback migration chưa chạy; migration/script chỉ được rà soát tĩnh trong phiên này.
- WARNING: full suite còn 41 lỗi trong Todo/TodoCategory cũ, cần xử lý riêng; không được tính là pass của toàn bộ repository.
- WARNING: migration index yêu cầu không có role names trùng không phân biệt hoa/thường.

## 9. Hướng dẫn migration và Swagger

1. Backup và xác minh connection string môi trường staging; kiểm tra trước `SELECT UPPER([Name]), COUNT(*) FROM Roles GROUP BY UPPER([Name]) HAVING COUNT(*) > 1`.
2. Review `Migrations/Phase4Rbac.sql`, chạy migration bằng quy trình triển khai staging đã phê duyệt. Không chạy script Down tự động; script sẽ từ chối nếu có grant mới phát sinh.
3. Xác minh 7 permission mới chỉ được gán Role Admin và Role ID/UserRoles/RolePermissions hiện hữu còn nguyên.
4. Khởi động API, mở Swagger UI theo cấu hình host (`/swagger`), xác thực bằng JWT của Admin có quyền trong DB, rồi gọi các endpoint trên. Kiểm tra user thường nhận 403.
5. Chạy test liên quan: `dotnet test BE-ZSM.Tests/BE-ZSM.Tests.csproj --filter FullyQualifiedName~RbacManagementTests` (cùng các filter Auth/PermissionAuthorization ở trên).

Không có database nào được cập nhật trong quá trình Phase 4.
