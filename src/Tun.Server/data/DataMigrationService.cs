using System.Text.Json;
using Microsoft.Extensions.Options;
using Tun.Contracts.Management;
using Tun.Server.Configuration;

namespace Tun.Server.Data;

/// <summary>
/// 数据迁移服务：从 JSON 文件迁移到 PostgreSQL
/// </summary>
public sealed class DataMigrationService
{
    private readonly TunnelRepository _repository;
    private readonly IOptions<TunnelServerOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DataMigrationService> _logger;

    public DataMigrationService(
        TunnelRepository repository,
        IOptions<TunnelServerOptions> options,
        IHostEnvironment environment,
        ILogger<DataMigrationService> logger)
    {
        _repository = repository;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// 从 JSON 文件迁移到数据库
    /// </summary>
    public async Task<MigrationResult> MigrateFromJsonAsync()
    {
        var result = new MigrationResult();

        try
        {
            var jsonPath = GetJsonPath();

            if (!File.Exists(jsonPath))
            {
                result.Success = false;
                result.Message = $"JSON 文件不存在: {jsonPath}";
                _logger.LogWarning(result.Message);
                return result;
            }

            // 读取 JSON 文件
            var json = await File.ReadAllTextAsync(jsonPath);
            var tunnels = JsonSerializer.Deserialize<List<ManagedTunnelConfig>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (tunnels == null || tunnels.Count == 0)
            {
                result.Success = true;
                result.Message = "JSON 文件为空，无需迁移";
                _logger.LogInformation(result.Message);
                return result;
            }

            // 批量插入数据库
            var inserted = await _repository.BulkInsertAsync(tunnels);
            result.Success = true;
            result.TotalCount = tunnels.Count;
            result.SuccessCount = inserted;
            result.Message = $"成功迁移 {inserted}/{tunnels.Count} 条记录";

            _logger.LogInformation("数据迁移完成: {Message}", result.Message);

            // 备份 JSON 文件
            var backupPath = $"{jsonPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(jsonPath, backupPath);
            _logger.LogInformation("已备份原 JSON 文件到: {BackupPath}", backupPath);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"迁移失败: {ex.Message}";
            _logger.LogError(ex, "数据迁移失败");
            return result;
        }
    }

    /// <summary>
    /// 从数据库导出到 JSON 文件（回滚用）
    /// </summary>
    public async Task<MigrationResult> ExportToJsonAsync()
    {
        var result = new MigrationResult();

        try
        {
            var tunnels = await _repository.GetAllAsync();
            var jsonPath = GetJsonPath();

            // 备份现有文件
            if (File.Exists(jsonPath))
            {
                var backupPath = $"{jsonPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(jsonPath, backupPath);
                _logger.LogInformation("已备份现有 JSON 文件到: {BackupPath}", backupPath);
            }

            // 写入 JSON 文件
            var json = JsonSerializer.Serialize(tunnels, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            await File.WriteAllTextAsync(jsonPath, json);

            result.Success = true;
            result.TotalCount = tunnels.Count;
            result.SuccessCount = tunnels.Count;
            result.Message = $"成功导出 {tunnels.Count} 条记录到 {jsonPath}";

            _logger.LogInformation("数据导出完成: {Message}", result.Message);
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"导出失败: {ex.Message}";
            _logger.LogError(ex, "数据导出失败");
            return result;
        }
    }

    private string GetJsonPath()
    {
        var configPath = _options.Value.ConfigPath;
        return Path.IsPathRooted(configPath)
            ? configPath
            : Path.Combine(_environment.ContentRootPath, configPath);
    }
}

/// <summary>
/// 迁移结果
/// </summary>
public sealed class MigrationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
}
