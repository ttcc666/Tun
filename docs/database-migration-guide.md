# Tun 数据库迁移指南

## 概述

本指南说明如何将 Tun 的数据存储从 JSON 文件迁移到 PostgreSQL 数据库。

## 架构变更

### 之前（JSON 文件）
- 数据存储在 `data/tunnels.json` 文件中
- 使用文件锁保证并发安全
- 每次修改都需要重写整个文件

### 之后（PostgreSQL）
- 数据存储在 PostgreSQL 数据库中
- 使用事务保证数据一致性
- 支持更高效的查询和更新
- 支持更大规模的隧道配置

## 数据库表结构

```sql
CREATE TABLE tun_tunnels (
    tunnel_id VARCHAR(100) PRIMARY KEY,
    client_id VARCHAR(100) NOT NULL,
    local_url VARCHAR(500) NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT true,
    description VARCHAR(500) NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);
```

## 配置步骤

### 1. 安装 PostgreSQL

确保已安装 PostgreSQL 数据库（版本 12+）。

```bash
# Ubuntu/Debian
sudo apt-get install postgresql postgresql-contrib

# macOS (使用 Homebrew)
brew install postgresql

# Windows
# 下载并安装 PostgreSQL from https://www.postgresql.org/download/windows/
```

### 2. 创建数据库

```bash
# 连接到 PostgreSQL
psql -U postgres

# 创建数据库
CREATE DATABASE tun;

# 创建用户（可选）
CREATE USER tun_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE tun TO tun_user;
```

### 3. 修改配置文件

编辑 `src/Tun.Server/appsettings.json`：

```json
{
  "Tun": {
    "Database": {
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=tun;Username=postgres;Password=your_password"
    }
  }
}
```

**配置说明**：
- `Enabled`: 设置为 `true` 启用数据库存储，`false` 则继续使用 JSON 文件
- `ConnectionString`: PostgreSQL 连接字符串

### 4. 启动服务

```bash
cd src/Tun.Server
dotnet run
```

首次启动时，SqlSugar 会自动创建表结构。

### 5. 数据迁移

#### 方式 1：通过 API（推荐）

服务启动后，调用迁移 API：

```bash
# 从 JSON 文件迁移到数据库
curl -X POST http://localhost:8080/api/migration/from-json \
  -H "X-Tun-Token: dev-token"

# 响应示例：
# {
#   "success": true,
#   "message": "成功迁移 3/3 条记录",
#   "totalCount": 3,
#   "successCount": 3
# }
```

**注意**：迁移成功后，原 JSON 文件会自动备份为 `tunnels.json.backup.yyyyMMddHHmmss`。

## 回滚到 JSON 文件

如果需要切换回 JSON 文件存储：

### 1. 导出数据库到 JSON

```bash
curl -X POST http://localhost:8080/api/migration/to-json \
  -H "X-Tun-Token: dev-token"
```

### 2. 修改配置文件

将 `appsettings.json` 中的 `Database.Enabled` 设置为 `false`。

### 3. 重启服务

## API 端点

### GET /api/config/tunnels
查询所有隧道配置（兼容 JSON 和数据库模式）

### POST /api/config/tunnels
创建或更新隧道配置（兼容 JSON 和数据库模式）

### DELETE /api/config/tunnels/{tunnelId}
删除隧道配置（兼容 JSON 和数据库模式）

### POST /api/migration/from-json
从 JSON 文件迁移到数据库（仅数据库模式可用）

### POST /api/migration/to-json
从数据库导出到 JSON 文件（仅数据库模式可用）

## 兼容性说明

- ✅ **完全向后兼容**：`ManagedTunnelStore` 自动检测配置模式
- ✅ **无缝切换**：可随时在 JSON 和数据库之间切换
- ✅ **API 不变**：客户端代码无需修改
- ✅ **Dashboard 兼容**：Dashboard 无需任何修改

## 总结

- ✅ 已添加 PostgreSQL + SqlSugar 支持
- ✅ 保持完全向后兼容
- ✅ 支持无缝迁移
- ✅ 提供迁移和回滚 API
- ✅ 自动备份原始数据
