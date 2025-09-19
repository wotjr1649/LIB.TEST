using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Lib.Db.Configuration;

/// <summary>
/// appsettings.json 의 ConnectionStrings 섹션을 <see cref="DbOptions"/>에 주입합니다.
/// </summary>
internal sealed class ConfigureDbOptions(IConfiguration configuration) : IConfigureOptions<DbOptions>
{
    private readonly IConfiguration _configuration = configuration;

    public void Configure(DbOptions options)
    {
        var section = _configuration.GetSection("ConnectionStrings");
        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                options.ConnectionStrings[child.Key] = child.Value!;
            }
        }

        var defaultName = options.DefaultConnectionName;
        if (!string.IsNullOrWhiteSpace(defaultName))
        {
            var defaultConnection = _configuration.GetConnectionString(defaultName);
            if (!string.IsNullOrWhiteSpace(defaultConnection))
            {
                options.ConnectionStrings[defaultName] = defaultConnection!;
            }
        }
    }
}
