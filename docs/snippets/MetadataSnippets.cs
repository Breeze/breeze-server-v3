// Snippets for docs/guide/metadata.md.

using Breeze.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Breeze.Docs.Snippets {

  [Route("breeze/[controller]/[action]")]
  public class MetadataController : Controller {
    private readonly NorthwindPersistenceManager _pm;

    public MetadataController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }

    #region MetadataAction
    [HttpGet]
    public IActionResult Metadata() => Ok(_pm.Metadata());
    #endregion
  }

  internal static class MetadataConfig {
    internal static void Enums() {
      #region UseIntEnums
      BreezeConfig.Instance.UseIntEnums = false;   // the default: strings
      #endregion
    }
  }
}
