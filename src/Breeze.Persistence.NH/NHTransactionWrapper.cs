using NHibernate;
using System.Data;

namespace Breeze.Persistence.NH {
  /// <summary>
  /// Presents an NHibernate ITransaction as an <see cref="IDbTransaction"/>, which is what the
  /// save pipeline in <see cref="PersistenceManager"/> works with.
  /// </summary>
  public class NHTransactionWrapper : IDbTransaction {
    ITransaction _itran;
    IDbConnection _connection;
    IsolationLevel _isolationLevel;

    internal NHTransactionWrapper(ITransaction itran, IDbConnection connection, IsolationLevel isolationLevel) {
      _itran = itran;
      _connection = connection;
      _isolationLevel = isolationLevel;
    }

    /// <summary> The connection this transaction runs on. </summary>
    public IDbConnection Connection { get { return _connection; } }

    /// <summary> The isolation level the transaction was begun with. </summary>
    public IsolationLevel IsolationLevel { get { return _isolationLevel; } }

    /// <summary> Commit the underlying NHibernate transaction. </summary>
    public void Commit() {
      _itran.Commit();
    }

    /// <summary> Roll back the underlying NHibernate transaction. </summary>
    public void Rollback() {
      _itran.Rollback();
    }

    /// <summary> Dispose the underlying NHibernate transaction.  The connection is left alone - this wrapper does not own it. </summary>
    public void Dispose() {
      _itran.Dispose();
    }

  }
}
