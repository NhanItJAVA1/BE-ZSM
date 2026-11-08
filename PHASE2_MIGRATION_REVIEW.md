# Review migration Phase 2 RBAC

Migration được review: `20261108122631_ConvertRoleNameToString`  
Phạm vi database: chỉ kiểm tra tĩnh. **Không kết nối hoặc thay đổi database ZSM.**

## Kết luận

### BLOCKER — cần xử lý trước khi chạy migration

1. **Không tương thích schema giữa backend cũ và schema mới.** Binary cũ ánh xạ `Role.Name` thành `int`; sau Up, cột là `nvarchar` và có giá trị `User`/`Admin`, do đó backend cũ không thể đọc dữ liệu theo model cũ. Backend mới cũng không hoạt động trên schema cũ sau khi `Role.Name` còn là `int`. Cần runbook rollout phối hợp: dừng toàn bộ instance backend cũ, backup, migrate, khởi động binary mới và xác nhận health trước khi nhận traffic. Chưa có đường nâng cấp zero-downtime được chứng minh.

### WARNING — chấp nhận hoặc xử lý theo vận hành

1. **Rollback sau khi tạo role động sẽ bị từ chối có chủ ý.** `Down` chỉ biểu diễn được `User`/`Admin` thành enum `0`/`1`; mọi tên role mới sẽ ném lỗi `51003` trước khi drop index hay đổi dữ liệu. Đây là bảo vệ dữ liệu, không phải rollback tổng quát. Sau khi tạo role động, chỉ rollback bằng kế hoạch chuyển đổi dữ liệu riêng; không xóa role/membership tự động.
2. **Chưa kiểm chứng bằng SQL Server thực tế.** Docker không khả dụng; `sqllocaldb info` thất bại khi truy cập cấu hình instance. `appsettings.Development.json` trỏ tới DB tên `ZSM`, nên không được dùng làm môi trường thử. Các kết luận database bên dưới là kiểm tra mã/script và tài liệu SQL Server, không phải kết quả chạy Up/Down.
3. `ALTER COLUMN` cần schema-modification lock; với bảng lớn hoặc workload đang hoạt động, có thể chặn request trong thời gian chuyển đổi. Microsoft xác nhận `ALTER TABLE` lấy Sch-M lock và thay đổi có thể ghi log/ảnh hưởng thời gian chạy ([ALTER TABLE — SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-table-transact-sql?view=sql-server-2017)).
4. Script sinh có lệnh bỏ default constraint trên `Roles.Name` nếu tồn tại. Trong migration lịch sử của repo, cột `Name` không có default constraint. Nếu DB có constraint được thêm thủ công, script bỏ nó và không khôi phục; cần kiểm kê schema thực tế trước khi áp dụng.
5. DDL Up guard kiểm tra int ngoài `0/1` trước đổi kiểu; duplicate check chạy sau ánh xạ và trước unique index. Đây là các bảo vệ trong script, nhưng chưa chạy các tình huống lỗi trên SQL Server thử nghiệm.

### PASS — đã xác minh tĩnh hoặc bằng build/test

1. Migration thay đổi cùng bảng `Roles`, không xóa/tạo lại bảng. `Id` không bị cập nhật. `Name=0` thành `User`; `Name=1` thành `Admin`, ánh xạ theo giá trị enum, không suy ra ID.
2. Không có FK, PK, index hiện hữu nào dựa trên `Roles.Name` trong migration tạo schema hiện tại. Các FK và khóa chính RBAC tham chiếu `Roles.Id`; `UserRoles` và `RolePermissions` không bị sửa bởi Up/Down. `Users.RoleId` và FK legacy cũng không đổi.
3. Cột `Name` không có default ở migration tạo Roles. Script do EF sinh kiểm tra/bỏ default constraint theo tên hệ thống trước `ALTER COLUMN`, xử lý được tên constraint do SQL Server tự đặt.
4. `ALTER COLUMN int -> nvarchar(50)` là chuyển đổi SQL Server hợp lệ theo quy tắc: kiểu cũ phải implicit-convertible sang kiểu mới; tài liệu Microsoft minh họa int gán sang varchar và mô tả tiêu chí chuyển kiểu ([Data type conversion](https://learn.microsoft.com/en-us/sql/t-sql/data-types/data-type-conversion-database-engine?view=sql-server-ver16), [ALTER TABLE](https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-table-transact-sql?view=sql-server-2017)). Với enum int 0/1 hiện tại, giá trị được biểu diễn thành chuỗi số rồi được ánh xạ sang tên.
5. Seed data trong model sau migration giữ nguyên Role ID 1/2 và tên User/Admin. Migration dùng SQL cập nhật dữ liệu hiện hữu, không dùng thao tác seed tạo lại row.
6. Cả script Up và Down được sinh bằng EF và kiểm tra tĩnh: [Up SQL](Migrations/ConvertRoleNameToString.sql), [Down SQL](Migrations/ConvertRoleNameToString.Down.sql). Down ánh xạ tên thành chuỗi số trước khi alter sang int.
7. Backend cũ sẽ đọc sai schema mới; vì vậy triển khai hai phiên bản lệch pha không được xem là tương thích. Backend mới truy vấn role legacy và memberships eager-loaded, không phát sinh N+1 trong các luồng đã review.
8. Trường DTO đơn `Role` nay lấy từ `User.Role.Name` (role gắn với `Users.RoleId`) để giữ hợp đồng legacy ổn định. `Roles` trả danh sách membership, sắp xếp theo tên; không chọn phần tử đầu tiên từ navigation collection không có thứ tự xác định. AuthService đã nạp cả legacy Role và memberships cho local login, external login, refresh; UserService nạp cả hai cho response.
9. JWT phát `ClaimTypes.Role` cho từng membership, lọc trùng không phân biệt hoa thường. Test xác nhận nhiều role, `IsInRole("Admin")`, mapping DTO legacy và plural roles, register, Google registration/login, refresh đọc membership mới nhất. Kết quả Auth tests: **11/11 pass**.
10. `dotnet build --no-restore`: **pass**, 0 warning/0 error. `dotnet ef migrations has-pending-model-changes`: **không có thay đổi model chờ**.
11. 41 lỗi full suite không phải lỗi RBAC theo bằng chứng hiện có: Todo test POST `/api/todos`, trong khi `TodoController` hiện chỉ khai báo GET và PUT `batch`, gây `405 MethodNotAllowed` trước khi kiểm tra quyền. Một lỗi TodoCategory khác là khác biệt status code (`403`/`404`) ở logic category. Các controller/service Todo không bị sửa trong lượt review này; không sửa ngoài phạm vi.

## Đã sửa trong lượt review

- Mapping trường `Role` đơn về role legacy `User.Role.Name`, thay vì lấy tùy ý membership đầu tiên.
- Bổ sung eager load legacy role song song memberships ở các truy vấn AuthService để mapping DTO có dữ liệu xác định.
- Tạo `Migrations/ConvertRoleNameToString.Down.sql` để review SQL rollback do EF sinh.

## Cần làm trước khi áp dụng thật

1. Kiểm tra trên SQL Server disposable/staging đã xác nhận, với snapshot dữ liệu đại diện: role ID 1/2, memberships, permissions, `Role.Name` ngoài 0/1, duplicate sau ánh xạ, default/index/FK.
2. Chạy cả Up và Down trên bản sao. Thử Down thêm một role động để xác nhận dừng an toàn và dữ liệu vẫn nguyên vẹn.
3. Chốt cửa sổ downtime/runbook dừng backend cũ, backup, migrate, khởi động backend mới. Không chạy script trực tiếp trên DB `ZSM` trước các bước này.
4. Nếu cần rollback sau khi role động đã được tạo, viết migration chuyển đổi role/membership riêng; không sửa guard để ép/xóa role.

