# Báo cáo Phase 2 Dynamic RBAC

## 1. Phần đã triển khai

- Authentication lấy danh sách role từ `UserRoles` khi đăng nhập local, Google Login và Refresh Token.
- Register local và tạo tài khoản Google thêm role `User` vào `UserRoles`, đồng thời tiếp tục ghi `Users.RoleId` trong giai đoạn tương thích. User, membership và external login được lưu trong cùng lần `SaveChangesAsync`; EF Core/SQL Server bao bọc lần lưu này trong transaction.
- JWT phát nhiều role claim cho mỗi role khác nhau (so sánh không phân biệt hoa thường), gồm `ClaimTypes.Role` để `Authorize(Roles = "Admin")` tiếp tục hoạt động và claim `role` cũ cho client tương thích.
- `UserResponseDto` giữ trường `Role` hiện tại, đồng thời thêm `Roles`. UserService nạp role memberships để mapping không bỏ sót role.
- `Role.Name` đổi sang `string`, cấu hình giới hạn 50 ký tự và unique index. Enum `BE_ZSM.Enums.UserRole` đã được loại bỏ sau khi rà soát không còn dependency source.
- Không thay đổi/xóa `Users.RoleId`.

## 2. File thay đổi

- Authentication/JWT: `Services/Auth/AuthService.cs`, `Services/JwtService.cs`.
- Entity/model/mapping/API DTO: `Entities/Role.cs`, `Contexts/AppDbContext.cs`, `Mappings/MappingProfile.cs`, `Services/User/UserService.cs`, `DTOs/Users/UserResponseDto.cs`, `Enums/UserRole.cs` (xóa).
- Tests: `BE-ZSM.Tests/AuthServiceTests.cs`, `BE-ZSM.Tests/TestApiFactory.cs`.
- Migration và script: `Migrations/20261108122631_ConvertRoleNameToString.cs`, file Designer tương ứng, `Migrations/AppDbContextModelSnapshot.cs`, `Migrations/ConvertRoleNameToString.sql`.
- Workspace đã có nhiều thay đổi Phase 1/RBAC từ trước ở các file khác. Những file đó được giữ nguyên, không được xem là thay đổi Phase 2 trong danh sách này.

## 3. Kiến trúc trước/sau

**Trước:** `AuthService` tải `User.Role` (một role) và JWT chỉ có một role. Register đặt `Users.RoleId` nhưng không tạo membership. `Role.Name` là enum được lưu dạng int.

**Sau:** `UserRoles` là nguồn role cho authentication và token; `Users.RoleId` tiếp tục được ghi như cột tương thích. Role claim được tạo từ membership hiện tại trong database ở mỗi lần đăng nhập/refresh. `Role.Name` là chuỗi; ID role không đổi.

## 4. Migration và SQL

Migration `20261108122631_ConvertRoleNameToString` chuyển đổi tại chỗ, không drop/recreate `Roles`:

- Chặn migration nếu có enum value khác `0` hoặc `1`.
- Đổi `0` thành `User`, `1` thành `Admin`, giữ nguyên ID.
- Kiểm tra trùng tên sau ánh xạ trước khi tạo unique index.
- Down kiểm tra chỉ có `User`/`Admin`, đổi về `0`/`1` trước khi đổi kiểu về int; dữ liệu không thể biểu diễn an toàn sẽ làm rollback dừng.
- SQL review nằm ở `Migrations/ConvertRoleNameToString.sql`.

Migration/script đã được tạo và rà soát tĩnh. Không chạy Up/Down trên database; do đó chưa xác nhận hành vi bằng SQL Server thực tế. Cần chạy trên bản sao/staging đã backup trước khi áp dụng thật.

## 5. Kết quả build/tests

- `dotnet build --no-restore`: **pass**, 0 warning, 0 error.
- `dotnet test ... --filter FullyQualifiedName~AuthServiceTests`: **pass**, 10/10.
- `dotnet ef migrations has-pending-model-changes`: **pass**, model snapshot khớp.
- Full suite: **25 pass, 41 fail / 66 tổng**. Phần lớn Todo integration tests hiện nhận `405 MethodNotAllowed` khi gọi endpoint; một test đọc response rỗng nên lỗi JSON. Đây là lỗi thuộc Todo/API backlog đã được nêu là tồn tại trước đó và không sửa trong phạm vi RBAC.
- Bộ test auth bao phủ local login/refresh, Google login/refresh, membership mặc định khi Google đăng ký, register không nhận role từ DTO, nhiều role và refresh phản ánh membership hiện tại.
- Chưa chạy migration Up/Down trên database; chưa có test tích hợp SQL Server cho kiểm tra constraint duplicate. `UserRoles` có composite primary key `(UserId, RoleId)` làm DB từ chối membership trùng.
- NuGet vulnerability feed không truy cập được trong môi trường (`NU1900` khi chạy test); không ảnh hưởng build/test đã dùng package restore có sẵn.

## 6. Lỗi phát hiện và cách xử lý

- EF scaffold migration mặc định chỉ đổi kiểu và seed, không bảo vệ role value lạ. Migration được sửa để kiểm tra dữ liệu trước chuyển đổi và chặn rollback không an toàn.
- Review Down migration phát hiện thứ tự sai có thể cố ép `User`/`Admin` sang int trước ánh xạ. Đã đổi thứ tự để lưu mã chuỗi `0`/`1` trước khi alter kiểu.
- Test Register ban đầu thất bại do mock AutoMapper không cấu hình map `RegisterUserDto`; mock được bổ sung.
- Full suite còn lỗi Todo nêu trên; không chỉnh sửa module ngoài phạm vi.

## 7. Security review

### Vòng 1 — Senior Backend Engineer

- Register và đăng ký Google lấy role mặc định từ database, không lấy role từ DTO/token client. Không có input để tự cấp Admin.
- User và membership được lưu trong cùng `SaveChangesAsync`; Google user, membership, external login cũng được lưu trong cùng lần save. Nếu save lỗi, không phát sinh user được commit một phần trên SQL Server.
- Login/refresh dùng Include membership và role trong một truy vấn eager load, tránh N+1.
- PK ghép `(UserId, RoleId)` chặn duplicate membership ở database. Một lần đọc membership không yêu cầu truy vấn cho từng role.
- Existing user liên kết Google giữ membership đang có; tài khoản mới được gán `User`.

### Vòng 2 — Security Reviewer

- Admin không được suy ra từ dữ liệu người dùng gửi; quyền token đến từ membership đã lưu.
- `ClaimTypes.Role` được giữ nên authorization hiện tại theo `Roles = "Admin"` nhận claim tương thích. Test xác minh User/Admin claims trong JWT.
- Refresh nạp membership hiện hành từ DB nên role đã thu hồi không được phát lại trong access token mới. JWT đã cấp trước đó vẫn hợp lệ tới thời điểm hết hạn vì hệ thống hiện là bearer JWT stateless, không có kiểm tra thu hồi từng request.
- Migration giữ nguyên Role ID và bảng Roles. Kiểm tra giá trị ngoài enum, duplicate sau ánh xạ, và kiểm tra Down trước chuyển đổi. Chưa chạy trên SQL Server nên cần xác minh bản sao trước khi rollout.
- User nhiều role được đưa đầy đủ vào JWT và trường DTO mới `Roles`; trường `Role` cũ vẫn giữ để tương thích client.
- Không thay đổi luồng cấp refresh token, expiry hoặc rotation. Các hành vi đó nằm ngoài yêu cầu Phase 2.

## 8. Blocker/rủi ro còn lại

- Migration Up/Down chưa được thực thi trên SQL Server do yêu cầu không tác động database thật; cần xác minh trên bản sao/staging.
- Full Todo test suite có 41 lỗi `405`/response như mô tả; chưa xác lập được bằng baseline chạy trước thay đổi trong lượt này.
- Access JWT đã phát không tự mất role ngay sau khi membership bị thu hồi; hiệu lực tối đa đến hạn JWT. Nếu cần thu hồi tức thời, cần thiết kế version/revocation hoặc authorization tra cứu server-side ở phase riêng.
- Các thay đổi Phase 1 đang có sẵn dưới dạng modified/untracked trong workspace; cần review chúng cùng bộ thay đổi trước khi commit.

## 9. Bước cần người dùng xác nhận

1. Review `Migrations/ConvertRoleNameToString.sql` và áp dụng migration trên staging/bản sao dữ liệu đã backup; kiểm tra Role ID 1/2 cùng dữ liệu membership sau migration.
2. Xác nhận có cần chính sách thu hồi quyền tức thời đối với access JWT đã phát hay chấp nhận hiệu lực đến expiry.
3. Xác nhận thời điểm và cách tách riêng migration loại bỏ `Users.RoleId`. Cột này chưa bị xóa.

## 10. Kế hoạch Phase 3

1. Dùng `RolePermissions`/`Permissions` để áp dụng authorization policy động thay vì chỉ role-name authorization.
2. Xây dựng API quản trị role, permission và membership với kiểm tra quyền chống tự nâng quyền.
3. Thiết kế audit log, cache invalidation và chính sách thu hồi JWT khi role/permission thay đổi.
4. Sau khi kiểm kê và migrate đầy đủ consumer, tạo migration riêng loại bỏ `Users.RoleId`; không gộp vào Phase 2.
5. Bổ sung test SQL Server cho migration Up/Down, duplicate membership, giữ nguyên role ID và các endpoint Admin.
