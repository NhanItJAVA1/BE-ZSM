# Báo cáo dọn dẹp test suite

## Kết quả

Đã chạy full suite trước cleanup: **91 tests, 50 pass, 41 fail**. Baseline TRX cho thấy toàn bộ 41 lỗi thuộc các test Todo cũ hoặc test TodoCategory phụ thuộc vào cách tạo Todo đã bị bỏ khỏi API hiện tại. Không có test Authentication, Google authentication, Permission Authorization hay RBAC Management nào thất bại.

Sau cleanup: **51 tests, 51 pass, 0 fail**.

## Tests đã xóa

Đã xóa toàn bộ bốn lớp chỉ kiểm thử API Todo cũ (38 tests):

- `TodoApiTests.cs` — 19 tests: dựa trên `POST /api/todos`, GET/PUT/DELETE theo ID và cập nhật trạng thái. `TodoController` hiện chỉ cung cấp `GET /api/todos` và `PUT /api/todos/batch`.
- `TodoQueryTests.cs` — 8 tests: dữ liệu fixture được tạo qua `POST /api/todos`, route không còn tồn tại.
- `TodoActivityTests.cs` — 6 tests: dựa trên tạo/cập nhật Todo và `/api/todos/{id}/activities`, các route đó không được expose bởi controller hiện tại.
- `TodoAuthorizationTests.cs` — 5 tests: kỳ vọng các route Todo theo ID/POST không còn tồn tại.

Trong `TodoCategoryApiTests.cs`, đã xóa 2 test phụ thuộc vào fixture Todo cũ:

- `DeleteCategory_WithDeleteTodosFalse_ShouldKeepTodosAndClearCategoryId`
- `DeleteCategory_WithDeleteTodosTrue_ShouldDeleteAssociatedTodos`

Đã bỏ helper `TodoRequest`, `CreateTodoAsync` và `GetSingleTodoByTitleAsync` khỏi `TestApiFactory`; không còn caller sau khi xóa các test trên.

## Test được giữ lại và kết quả kiểm tra

Giữ nguyên toàn bộ test Authentication, Google Login, Permission Authorization và RBAC Management:

- `AuthServiceTests`: 11/11 pass.
- `GoogleAuthProviderTests`: 5/5 pass.
- `PermissionAuthorizationTests`: 12/12 pass.
- `RbacManagementTests`: 12/12 pass.
- `TodoCategoryApiTests`: còn 11 tests; tất cả pass.

Cả 10 test TodoCategory đang pass trước cleanup đều được giữ. Test `UpdateCategory_AnotherUsersCategory` cũng được giữ: `TodoCategoryService.UpdateCategoryAsync` hiện chủ đích trả Forbidden khi category thuộc user khác, vì vậy expectation được cập nhật từ 404 sang 403. Đây không mở dữ liệu cho caller và khớp với implementation hiện tại.

## Build và test

- `dotnet build --no-restore --configuration Release`: PASS, 0 warning, 0 error.
- `dotnet test BE-ZSM.Tests/BE-ZSM.Tests.csproj --no-restore --no-build --configuration Release`: PASS, 51/51.
- Lệnh `dotnet test ... --no-restore --configuration Release` của workflow cũng đã build test project Release và hoàn thành; chạy xác nhận riêng với `--no-build` cho kết quả đầy đủ 51/51.
- Restore trong lần test phát cảnh báo `NU1900`: môi trường không truy cập được NuGet vulnerability feed. Không ảnh hưởng build hoặc kết quả test.

## CI/CD

Trong `.github/workflows/deploy.yml`, restore giờ nhắm tới `BE-ZSM.Tests/BE-ZSM.Tests.csproj`, bao gồm project reference backend. Job build thêm bước test Release. Docker và deploy phụ thuộc vào build job nên sẽ chỉ chạy sau khi build và test thành công. Workflow này chỉ trigger trên push tới `main`, như cấu hình hiện hữu.

## Phạm vi và lưu ý

Không thay đổi business logic, route hoặc API. Authentication và toàn bộ test RBAC vẫn nguyên vẹn. Không commit hoặc push.
