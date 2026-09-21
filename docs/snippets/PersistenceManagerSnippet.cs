// The PersistenceManager subclass shown in getting-started.md and persistence-manager.md.

namespace Breeze.Docs.Snippets {
  #region PersistenceManager
  using Breeze.Persistence.EFCore;

  public class NorthwindPersistenceManager : EFPersistenceManager<NorthwindContext> {
    public NorthwindPersistenceManager(NorthwindContext context) : base(context) { }
  }
  #endregion
}
