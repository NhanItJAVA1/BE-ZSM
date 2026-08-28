<div align="center">

# 🏁 ZSM Record

Nền tảng chia sẻ và quản lý thành tích đua ZingSpeed Mobile.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react)](https://react.dev/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Docker](https://img.shields.io/badge/Docker-Enabled-2496ED?logo=docker)](https://www.docker.com/)

</div>

---

## 📖 Giới thiệu

**ZSM Record** cho phép người chơi đăng tải video, lưu thành tích và khám phá các kỷ lục trong ZingSpeed Mobile.

## ✨ Tính năng

- Đăng ký và đăng nhập bằng JWT
- Đăng tải video thành tích
- Lọc thành tích theo bản đồ, xe và chế độ chơi
- Xét duyệt hoặc từ chối bài đăng
- Gợi ý phương tiện phù hợp với từng bản đồ
- Quản lý công việc bằng Todo List
- Theo dõi lịch sử thay đổi công việc

## 🛠 Công nghệ sử dụng

### Backend

- ASP.NET Core
- Entity Framework Core
- SQL Server
- Redis
- AWS S3
- JWT Authentication
- Docker

### Frontend

- React + TypeScript
- Vite
- Tailwind CSS
- React Query
- Redux Toolkit
- React Hook Form + Zod

## 🏗 Kiến trúc

```text
Controller
    ↓
Service
    ↓
Unit of Work
    ↓
Repository
    ↓
SQL Server
