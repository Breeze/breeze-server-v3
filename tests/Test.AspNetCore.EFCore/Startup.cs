// Either EFCORE or NHIBERNATE should be defined in the project properties

using Breeze.AspNetCore;
using Breeze.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inheritance.Models;
using Test.AspNetCore.Controllers;
using Breeze.Persistence;


#if EFCORE
using Microsoft.EntityFrameworkCore;
using Models.NorthwindIB.CF;
using ProduceTPH;
#elif NHIBERNATE
using Breeze.Persistence.NH;
#endif

namespace Test.AspNetCore {
  public class Startup {

    public Startup(IConfiguration configuration) {
      Configuration = configuration;
    }
    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940


    // This method gets called by the runtime. Use this method to add services to the container.
    /// <summary>Named CORS policy used by the browser-based client test runner.</summary>
    public const string TestCorsPolicy = "BreezeTestCors";

    public void ConfigureServices(IServiceCollection services) {
      // The client suite runs in a browser (Vitest browser mode) served from a
      // different origin, so every request here is cross-origin.
      // AllowAnyOrigin cannot be used: the breeze fetch adapter sends
      // credentials: 'include', and browsers reject a wildcard
      // Access-Control-Allow-Origin on a credentialed request. Reflecting the
      // caller's origin is the way to allow both.
      // This is a TEST server. Do not copy this policy into a real application.
      services.AddCors(options => options.AddPolicy(TestCorsPolicy, policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        .WithExposedHeaders("X-HTTP-Method-Override")));

      services.AddMvc(option => option.EnableEndpointRouting = false);
      var mvcBuilder = services.AddMvc();
      // Tests use string enums
      BreezeConfig.Instance.UseIntEnums = false;
      services.AddControllers().AddNewtonsoftJson(opt => {
        var ss = JsonSerializationFns.UpdateWithDefaults(opt.SerializerSettings, false, BreezeConfig.Instance.UseIntEnums);

#if NHIBERNATE
        // NHibernate settings
        var settings = opt.SerializerSettings;
        settings.ContractResolver = NHibernateContractResolver.Instance;

        settings.Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args) {
          // When the NHibernate session is closed, NH proxies throw LazyInitializationException when
          // the serializer tries to access them.  We want to ignore those exceptions.
          var error = args.ErrorContext.Error;
          if (error is NHibernate.LazyInitializationException || error is System.ObjectDisposedException)
            args.ErrorContext.Handled = true;
        };

        if (!settings.Converters.Any(c => c is NHibernateProxyJsonConverter)) {
          settings.Converters.Add(new NHibernateProxyJsonConverter());
        }
#endif

      });


      mvcBuilder.AddMvcOptions(o => { o.Filters.Add(new GlobalExceptionFilter()); });

      // All test models share a single database (BreezeTestDb).
      // See Tests/Databases/README.md for how to create it.
      var conn = Configuration.GetConnectionString("BreezeTestDb");
#if EFCORE
#if NET8_0_OR_GREATER
      services.AddDbContext<InheritanceContext>(options => options.UseSqlServer(conn, o => o.UseCompatibilityLevel(120)));
      services.AddDbContext<NorthwindIBContext_CF>(options => options.UseSqlServer(conn, o => o.UseCompatibilityLevel(120)));
      services.AddDbContext<ProduceTPHContext>(options => options.UseSqlServer(conn, o => o.UseCompatibilityLevel(120)));
#else
      services.AddDbContext<InheritanceContext>(options => options.UseSqlServer(conn));
      services.AddDbContext<NorthwindIBContext_CF>(options => options.UseSqlServer(conn));
      services.AddDbContext<ProduceTPHContext>(options => options.UseSqlServer(conn));
#endif
#endif

#if NHIBERNATE
      services.AddSingleton(provider =>
        BuildFactory<InheritancePersistenceManager>(conn, typeof(Inheritance.Models.AccountType).Assembly));
      services.AddSingleton(provider =>
        BuildFactory<NorthwindPersistenceManager>(conn, typeof(Models.NorthwindIB.NH.Customer).Assembly));
      services.AddSingleton(provider =>
        BuildFactory<ProducePersistenceManager>(conn, typeof(Models.Produce.NH.ItemOfProduce).Assembly));
#endif
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
#if EFCORE
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, InheritanceContext inheritanceContext) {
      InheritanceDbInitializer.Seed(inheritanceContext);
#elif NHIBERNATE
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, NHSessionProvider<InheritancePersistenceManager> provider) {
      var persistenceManager = new InheritancePersistenceManager(provider);
      persistenceManager.Seed();
#endif

      if (env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
      }

      // allows use of html startup file.
      // This code assumes that the 'breezeTests' dir has all of the breeze test files and a copy of the breeze.debug.js file in a breeze subdir.
      var path = Path.Combine(Directory.GetCurrentDirectory(), @"breezeTests");
      // Must come before UseMvc so preflight requests are answered.
      app.UseCors(TestCorsPolicy);

      app.UseStaticFiles(new StaticFileOptions() {
        FileProvider = new PhysicalFileProvider(path),
        RequestPath = new PathString("")
      });

      app.UseMvc();

    }
    //public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
    //    if (env.IsDevelopment()) {
    //        app.UseDeveloperExceptionPage();
    //    }

    //    app.UseRouting();

    //    app.UseEndpoints(endpoints =>
    //    {
    //        endpoints.MapGet("/", async context =>
    //        {
    //            await context.Response.WriteAsync("Hello World!");
    //        });
    //    });
    //}


#if NHIBERNATE
    // Build NHibernate ISessionFactory, then wrap it in NHSessionProvider<T>
    NHSessionProvider<T> BuildFactory<T>(string connectionString, System.Reflection.Assembly modelAssembly) {
      var cfg = new NHibernate.Cfg.Configuration();
      cfg.DataBaseIntegration(db => {
        db.ConnectionString = connectionString;
        db.Dialect<NHibernate.Dialect.MsSql2008Dialect>();
        db.Driver<NHibernate.Driver.MicrosoftDataSqlClientDriver>();
        db.LogFormattedSql = true;
        db.LogSqlInConsole = true;
      });
      cfg.Properties.Add(NHibernate.Cfg.Environment.DefaultBatchFetchSize, "32");
      cfg.CurrentSessionContext<NHibernate.Context.ThreadStaticSessionContext>();
      //var modelAssembly = typeof(Models.NorthwindIB.NH.Customer).Assembly;
      cfg.AddAssembly(modelAssembly);  // mapping is in this assembly

      var sessionFactory = cfg.BuildSessionFactory();
      return new NHSessionProvider<T>(sessionFactory);
    }
#endif

  }

}
