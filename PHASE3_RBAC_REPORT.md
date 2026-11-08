# Báo cáo Phase 3 — Dynamic Permission-Based Authorization

## 1. Kiến trúc authorization

- `[HasPermission(PermissionCodes.X)]` tạo policy `permission:<code>` qua `PermissionAuthorizationPolicyProvider`; provider chuyển policy thường về `DefaultAuthorizationPolicyProvider`.
- Policy yêu cầu authenticated user. `PermissionAuthorizationHandler` lấy UserId duy nhất từ claim `NameIdentifier` của principal đã xác thực; không đọc UserId từ route/body/query và không xét role/permission claims.
- Handler chạy một truy vấn EF Core `Users.AnyAsync(...)` đi qua `UserRoles → RolePermissions → Permissions`. Điều này vừa yêu cầu user tồn tại, vừa yêu cầu membership và permission thực tế. User nhiều role nhận union permission theo `Any`; không có cache nên thu hồi có hiệu lực ở request kế tiếp.
- Handler/DbContext scoped; policy provider singleton không phụ thuộc scoped service. Cancellation token lấy từ `HttpContext.RequestAborted`. Mã permission lạ, user id sai, user/membership/permission thiếu đều fail closed.
- Không có quyền mặc định cho Admin trong handler. Admin chỉ có quyền nếu RolePermissions trong DB cấp permission.

## 2. File Phase 3 thay đổi

- Authorization: `Authorization/PermissionCodes.cs`, `PermissionRequirement.cs`, `PermissionAuthorizationHandler.cs`, `PermissionAuthorizationPolicyProvider.cs`, `HasPermissionAttribute.cs`.
- DI/pipeline và endpoint: `Program.cs`, `Controllers/TodoController.cs`, `TodoCategoryController.cs`, `CatalogController.cs`, `UsersController.cs`.
- Ownership: `Services/TodoService/TodoService.cs`.
- Seed/migration: `Migrations/20261108142114_SeedDynamicPermissions.cs`, Designer tương ứng, `Migrations/SeedDynamicPermissions.sql`, `Migrations/SeedDynamicPermissions.Down.sql`.
- Tests: `BE-ZSM.Tests/PermissionAuthorizationTests.cs`, `BE-ZSM.Tests/TestApiFactory.cs`.

## 3. Permission codes và endpoint mapping

| Permission | Endpoint/function | Role mặc định |
|---|---|---|
| `todo.read` | `GET /api/todos` | User, Admin |
| `todo.write` | `PUT /api/todos/batch` (create/update/delete trong cùng endpoint) | User, Admin |
| `todo-category.read` | `GET /api/todo-categories`, `GET /api/todo-categories/{id}` | User, Admin |
| `todo-category.create` | `POST /api/todo-categories` | User, Admin |
| `todo-category.update` | `PUT /api/todo-categories/{id}` | User, Admin |
| `todo-category.delete` | `DELETE /api/todo-categories/{id}` | User, Admin |
| `catalog.upload` | `POST /api/catalog/image-upload-url` | Admin |
| `users.read` | `GET /api/users`, `GET /api/users/{id}` | Admin |
| `users.update` | `PUT /api/users/{id}` | Admin |
| `users.delete` | `DELETE /api/users/{id}` | Admin |

`AuthController` giữ các luồng xác thực công khai hiện có. UsersController trước đây không có authorization; các endpoint đọc/sửa/xóa user nay yêu cầu permission admin phù hợp. Đây là siết quyền chủ đích; client quản trị phải có membership Admin trong DB và mapping tương ứng.

## 4. Seed data và migration

Migration `20261108142114_SeedDynamicPermissions` yêu cầu role có tên `User` và `Admin`, không tạo/sửa/xóa Role hoặc UserRole. Nó seed 10 permission code và 16 grant RolePermission: User nhận 6 quyền Todo/category; Admin nhận cả 10 quyền bao gồm catalog/users. Không có permission quản trị nào gán cho User mặc định.

Seed DML có `NOT EXISTS`, giữ nguyên permission/grant đã có. Bảng phụ `RbacPermissionSeedHistory` ghi chính xác permission và grant được migration tạo. Down chỉ xóa các row đã ghi nhận; nếu một permission do migration tạo có thêm grant về sau, rollback ném lỗi trước khi xóa để tránh làm mất cấu hình. Permission/grant tồn tại từ trước được giữ nguyên. UserRoles, `Users.RoleId`, Role ID và RolePermissions không liên quan đến seed không bị thay đổi.

Chỉ sinh script review, không chạy `database update`. Hướng dẫn rollout:

1. Xác nhận DB đã có Phase 1/2, backup và kiểm tra tên role User/Admin cùng unique code.
2. Chạy migration trên đúng target bằng connection staging/production được chọn tường minh trong pipeline quản trị; không dựa vào connection string mặc định của workspace.
3. Xác minh có 10 permission dự kiến, 6 grant cho User, 10 grant cho Admin và history table có đúng số row do migration thêm.
4. Deploy backend Phase 3 sau khi seed thành công. Backend Phase 2 có thể tiếp tục hoạt động khi migration được áp dụng vì migration chỉ thêm rows/table mà không đổi schema mà Phase 2 đọc.
5. Nếu cần rollback, trước tiên dừng backend Phase 3; chạy Down chỉ khi migration guard cho phép. Không xóa grant mới để ép rollback.

## 5. Kết quả kiểm thử

- `dotnet build --no-restore`: **pass**, 0 warning/0 error.
- Permission integration tests: **12/12 pass** — 401 không JWT; 403 thiếu quyền; 200 có quyền; union nhiều role; membership/RolePermission thu hồi áp dụng request tiếp theo; JWT claim Admin không đủ khi DB không cấp; DB permission cho phép; user không có membership/nonexistent user fail closed; ownership Todo và category.
- Authentication tests: **11/11 pass**.
- `dotnet ef migrations has-pending-model-changes`: **pass**, không có thay đổi model chờ.
- Full suite: **38 pass, 41 fail / 79**. Failures hiện vẫn thuộc Todo API tests hiện có: tests POST `/api/todos` trong khi `TodoController` chỉ khai báo GET và PUT `batch` (405), cộng kỳ vọng TodoCategory `404` nhưng service hiện trả `403`. Không đổi route/method hoặc sửa các test cũ. Permission-specific tests xác nhận route hiện có.
- Test restore báo `NU1900` do không truy cập được NuGet vulnerability feed; build/test dùng dependencies đã có.

## 6. Security review

### Vòng 1 — Senior Backend Engineer

- Truy vấn quyền là một correlated `AnyAsync`, không tải collection hoặc chạy N+1; `AsNoTracking` phù hợp truy vấn kiểm tra.
- Scoped handler dùng scoped DbContext; policy provider singleton không giữ state request.
- HasPermission policy yêu cầu authenticated user. Default/fallback policy ASP.NET không bị nới lỏng; `[Authorize]` hiện có trên Todo controllers vẫn được giữ thêm lớp authentication.
- Quyền User sau seed giữ khả năng dùng Todo/category như trước; Catalog giữ hạn chế Admin; UsersController được bảo vệ thay vì để công khai.
- Todo batch kiểm tra ownership cho ID update/delete và kiểm tra category ownership trước khi ghi. Không attach entity update từ client để ghi đè UserId; update lên Todo không thuộc user trả 404, và category của user khác không thể gắn vào Todo.

### Vòng 2 — Security Reviewer

- Claims `Admin` hoặc `role` trong JWT không tham gia quyết định permission; test token có Admin claim nhưng DB thiếu mapping trả 403.
- UserId lấy từ `NameIdentifier` đã xác thực; user không tồn tại hoặc không có membership không được cấp quyền.
- Không cache permission result; test thu hồi membership/RolePermission chứng minh request kế tiếp bị từ chối.
- Role/User/Permission union không dựa vào một role đầu tiên; quyền đến từ mọi membership.
- Ownership vẫn tách khỏi function permission; test xác nhận Todo của user khác không bị sửa và category người khác không bị liên kết.
- Không còn policy `AdminOnly` cũ dựa trên role claim. Không có permission ngầm cho Admin ngoài các grant được seed.

## 7. Vấn đề còn lại

- Không có SQL Server disposable khả dụng trong môi trường: Docker không hiện diện; `sqllocaldb info` lỗi khi đọc cấu hình instance. Vì vậy migration Up/Down chỉ được kiểm tra tĩnh qua file migration/script, không tuyên bố đã chạy thực tế.
- Trong lúc xem danh sách migration, `dotnet ef migrations list --no-build` đã thử kết nối read-only qua `DefaultConnection` cấu hình sẵn rồi thất bại transient trước khi đọc migration history. Không có kết nối thành công và không có thao tác ghi/update; không chạy lại lệnh đó.
- User update/delete permissions đang áp dụng toàn cục cho user records; ownership/self-service policy không thuộc scope RBAC Phase 3 này. Admin-only grants không cho User mặc định truy cập.
- `todo.write` phản ánh endpoint batch gộp nhiều hành động. Nếu sau này tách API create/update/delete, nên chia permission theo endpoint khi thực sự có route tương ứng.

## 8. Trạng thái hoàn tất

Phần code và tests an toàn đã triển khai. Migration/script cần được review và chạy trên DB mục tiêu qua quy trình vận hành có backup; không có database nào được cập nhật trong lượt này.
