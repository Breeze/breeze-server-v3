// Snippets for docs/guide/getting-started.md.

using Breeze.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Breeze.Docs.Snippets {

  internal static class GettingStartedStartup {

    internal static void ConfigureJson(WebApplicationBuilder builder) {
      #region ConfigureJson
      builder.Services.AddControllers().AddNewtonsoftJson(opt => {
        JsonSerializationFns.UpdateWithDefaults(opt.SerializerSettings);
      });
      #endregion
    }

    internal static void AddDbContext(WebApplicationBuilder builder) {
      #region AddDbContext
      builder.Services.AddDbContext<NorthwindContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("Northwind")));
      #endregion
    }
  }
}
